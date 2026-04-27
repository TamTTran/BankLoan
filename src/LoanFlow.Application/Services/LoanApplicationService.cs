using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.LoanApplication;
using LoanFlow.Application.DTOs.LoanProduct;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public interface ILoanApplicationService
{
    Task<PagedResult<LoanApplicationDto>> GetListAsync(ApplicationFilterRequest filter);
    Task<Result<LoanApplicationDto>> GetByIdAsync(int id);
    Task<Result<LoanApplicationDto>> CreateAsync(CreateApplicationRequest request, int userId);
    Task<Result<LoanApplicationDto>> UpdateAsync(int id, UpdateApplicationRequest request, int userId);
    Task<Result> SubmitAsync(int id, int userId);
    Task<Result> ApproveAsync(int id, int approverUserId);
    Task<Result> RejectAsync(int id, RejectApplicationRequest request, int approverUserId);
    Task<Result<InstallmentPreviewDto>> PreviewAsync(InstallmentPreviewRequest request);
}

public class LoanApplicationService : ILoanApplicationService
{
    private readonly LoanFlowDbContext _db;
    private readonly ILoanProductService _productService;
    private readonly IApprovalRuleService _approvalRuleService;
    private readonly ICicService _cicService;

    public LoanApplicationService(LoanFlowDbContext db, ILoanProductService productService, IApprovalRuleService approvalRuleService, ICicService cicService)
    {
        _db = db;
        _productService = productService;
        _approvalRuleService = approvalRuleService;
        _cicService = cicService;
    }

    public async Task<PagedResult<LoanApplicationDto>> GetListAsync(ApplicationFilterRequest filter)
    {
        var query = _db.LoanApplications
            .Include(a => a.Customer)
            .Include(a => a.Product)
            .Include(a => a.CreatedBy)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(a =>
                a.Customer.FullName.ToLower().Contains(s) ||
                a.Customer.Phone.Contains(s));
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(a => a.Status == filter.Status);
        if (filter.FromDate.HasValue)
            query = query.Where(a => a.CreatedAt >= filter.FromDate);
        if (filter.ToDate.HasValue)
            query = query.Where(a => a.CreatedAt <= filter.ToDate.Value.AddDays(1));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return new PagedResult<LoanApplicationDto>
        {
            Items = items.Select(MapToDto),
            TotalCount = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<Result<LoanApplicationDto>> GetByIdAsync(int id)
    {
        var a = await _db.LoanApplications
            .Include(x => x.Customer)
            .Include(x => x.Product)
            .Include(x => x.CreatedBy)
            .Include(x => x.Decisions).ThenInclude(d => d.DecidedBy)
            .Include(x => x.Loan)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (a is null) return Result.Failure<LoanApplicationDto>("APP-VAL-003: Application not found.");
        return Result.Success(MapToDto(a));
    }

    public async Task<Result<LoanApplicationDto>> CreateAsync(CreateApplicationRequest req, int userId)
    {
        // APP-VAL-003
        var customer = await _db.Customers.FindAsync(req.CustomerId);
        if (customer is null) return Result.Failure<LoanApplicationDto>("APP-VAL-003: Customer not found.");
        if (customer.Status != "Active") return Result.Failure<LoanApplicationDto>("Customer is not active.");

        // Digital Finance Policy: Block loan origination without biometrics
        if (customer.EkycStatus != "Verified")
            return Result.Failure<LoanApplicationDto>("APP-VAL-005: Khách hàng chưa hoàn tất sinh trắc học eKYC theo chuẩn tài chính số.");

        // Exposure Limit Check
        var limit = await _db.ExposureLimits.FirstOrDefaultAsync(e => e.CustomerId == req.CustomerId);
        decimal maxExposure = limit?.MaxExposure ?? 500000000m; // 500M default if not configured

        var currentExposure = await _db.Loans
            .Where(l => l.CustomerId == req.CustomerId && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue))
            .SumAsync(l => l.OutstandingAmount);

        if (currentExposure + req.RequestedAmount > maxExposure)
            return Result.Failure<LoanApplicationDto>($"APP-VAL-006: Requested amount exceeds customer exposure limit. Max: {maxExposure:N0}, Current: {currentExposure:N0}, Requested: {req.RequestedAmount:N0}");

        // APP-VAL-004
        var product = await _db.LoanProducts.FindAsync(req.ProductId);
        if (product is null) return Result.Failure<LoanApplicationDto>("APP-VAL-004: Product not found.");
        if (product.Status != "Active") return Result.Failure<LoanApplicationDto>("APP-VAL-004: Product is not active.");

        // APP-VAL-001
        if (req.RequestedAmount < product.MinAmount || req.RequestedAmount > product.MaxAmount)
            return Result.Failure<LoanApplicationDto>("APP-VAL-001: Requested amount is outside product range.");

        // APP-VAL-002
        var allowedTenors = product.GetAllowedTenors();
        if (!allowedTenors.Contains(req.RequestedTenorMonths))
            return Result.Failure<LoanApplicationDto>("APP-VAL-002: Requested tenor is not allowed for this product.");

        var entity = new LoanApplication
        {
            CustomerId = req.CustomerId,
            ProductId = req.ProductId,
            RequestedAmount = req.RequestedAmount,
            RequestedTenorMonths = req.RequestedTenorMonths,
            Purpose = req.Purpose,
            Remarks = req.Remarks,
            Status = ApplicationStatus.Draft,
            CreatedByUserId = userId
        };
        _db.LoanApplications.Add(entity);
        await _db.SaveChangesAsync();

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.ApplicationCreated,
            EntityType = "Application",
            EntityId = entity.Id,
            NewValuesJson = AuditHelper.ToJson(new { customerId = entity.CustomerId, productId = entity.ProductId, amount = entity.RequestedAmount })
        });
        await _db.SaveChangesAsync();

        return await GetByIdAsync(entity.Id);
    }

    public async Task<Result<LoanApplicationDto>> UpdateAsync(int id, UpdateApplicationRequest req, int userId)
    {
        var entity = await _db.LoanApplications
            .Include(a => a.Product)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (entity is null) return Result.Failure<LoanApplicationDto>("Application not found.");

        // §12.3 Draft applications can be edited; submitted/approved/rejected cannot
        if (entity.Status != ApplicationStatus.Draft)
            return Result.Failure<LoanApplicationDto>("APP-SUB-001: Only draft application can be edited.");

        var product = entity.Product;
        if (req.RequestedAmount < product.MinAmount || req.RequestedAmount > product.MaxAmount)
            return Result.Failure<LoanApplicationDto>("APP-VAL-001: Requested amount is outside product range.");

        var allowedTenors = product.GetAllowedTenors();
        if (!allowedTenors.Contains(req.RequestedTenorMonths))
            return Result.Failure<LoanApplicationDto>("APP-VAL-002: Requested tenor is not allowed.");

        entity.RequestedAmount = req.RequestedAmount;
        entity.RequestedTenorMonths = req.RequestedTenorMonths;
        entity.Purpose = req.Purpose;
        entity.Remarks = req.Remarks;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.ApplicationUpdated,
            EntityType = "Application",
            EntityId = entity.Id
        });
        await _db.SaveChangesAsync();

        return await GetByIdAsync(id);
    }

    public async Task<Result> SubmitAsync(int id, int userId)
    {
        var entity = await _db.LoanApplications
            .Include(a => a.Customer)
            .Include(a => a.Product)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (entity is null) return Result.Failure("Application not found.");

        // APP-SUB-001
        if (entity.Status != ApplicationStatus.Draft)
            return Result.Failure($"APP-SUB-001: Only draft application can be submitted.");

        // APP-SUB-002: customer and product must be active
        if (entity.Customer.Status != "Active") return Result.Failure("APP-SUB-002: Customer is not active.");
        if (entity.Product.Status != "Active") return Result.Failure("APP-SUB-002: Product is not active.");

        // DTI Check
        if (entity.Customer.MonthlyIncome > 0)
        {
            var previewReq = new InstallmentPreviewRequest 
            { 
                ProductId = entity.ProductId, 
                Amount = entity.RequestedAmount, 
                TenorMonths = entity.RequestedTenorMonths 
            };
            var previewRes = await _productService.PreviewInstallmentAsync(previewReq);
            if (previewRes.IsSuccess)
            {
                var newEmi = previewRes.Value.MonthlyInstallment;
                
                var activeLoans = await _db.Loans
                    .Include(l => l.RepaymentSchedules)
                    .Where(l => l.CustomerId == entity.CustomerId && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue))
                    .ToListAsync();
                    
                var existingEmi = activeLoans.Sum(l => 
                {
                    var firstUnpaid = l.RepaymentSchedules.FirstOrDefault(s => s.Status != InstallmentStatus.Paid);
                    return firstUnpaid != null ? firstUnpaid.PrincipalDue + firstUnpaid.InterestDue : 0m;
                });
                // DTI Check (Module 3.1)
                var dti = (existingEmi + newEmi) / entity.Customer.MonthlyIncome.Value;
                
                var maxDtiConfig = await _db.SystemConfigs.FirstOrDefaultAsync(s => s.Key == "MaxDTI");
                decimal maxDti = maxDtiConfig != null && decimal.TryParse(maxDtiConfig.Value, out var v) ? v : 0.50m; // Changed default to 50%
                
                if (dti > maxDti)
                    return Result.Failure($"APP-VAL-007: DTI limit exceeded. Max: {maxDti:P0}, Current: {dti:P0}.");
            }
        }

        // LTV Check (Module 3.2)
        if (entity.Product.RequiresCollateral && entity.Product.MaxLtvRatio.HasValue)
        {
            // Lấy tổng tài sản bảo đảm của KH chưa được gán cho khoản vay nào (hoặc dùng tạm tổng TSĐB của KH)
            // Trong thực tế sẽ cần ApplicationCollateral mapping, nhưng ở đây ta check tạm tổng TSĐB
            var totalCollateralValue = await _db.Collaterals
                .Where(c => c.CustomerId == entity.CustomerId && c.Status == "Active")
                .SumAsync(c => c.AppraisedValue);

            var allocatedValue = await _db.LoanCollaterals
                .Include(lc => lc.Collateral)
                .Where(lc => lc.Collateral.CustomerId == entity.CustomerId && lc.Status == "Active")
                .SumAsync(lc => lc.AllocatedValue);

            var availableCollateralValue = totalCollateralValue - allocatedValue;

            if (availableCollateralValue <= 0)
                return Result.Failure("APP-VAL-008: Customer has no available collateral for this secured product.");

            var requestedLtv = entity.RequestedAmount / availableCollateralValue;

            if (requestedLtv > entity.Product.MaxLtvRatio.Value)
                return Result.Failure($"APP-VAL-009: LTV limit exceeded. Max LTV: {entity.Product.MaxLtvRatio.Value:P0}, Requested LTV: {requestedLtv:P0}.");
        }

        // DFI-STP-001: Straight-Through Processing (Auto-Approve via Rule Engine)
        var requiredRules = (await _approvalRuleService.GetRequiredRulesForApplicationAsync(id)).ToList();
        var stpRule = requiredRules.FirstOrDefault(r => r.IsAutoApprove);

        if (stpRule != null && entity.Customer.EkycStatus == "Verified")
        {
            var cicRes = await _cicService.InquireCicAsync(entity.CustomerId, userId);
            if (cicRes.IsSuccess && cicRes.Value.CicScore >= stpRule.MinCicScore)
            {
                entity.Status = ApplicationStatus.Approved;
                
                _db.ApplicationDecisions.Add(new ApplicationDecision
                {
                    ApplicationId = id,
                    Decision = "Approved",
                    DecidedByUserId = userId, // Auto trigger by the one submitting
                    DecisionReason = $"Auto-Approved STP (CIC: {cicRes.Value.CicScore} >= {stpRule.MinCicScore})"
                });

                _db.AuditLogs.Add(new AuditLog
                {
                    ActorUserId = userId,
                    ActionCode = "AUTO_APPROVED",
                    EntityType = "Application",
                    EntityId = entity.Id,
                    NewValuesJson = AuditHelper.ToJson(new { cicScore = cicRes.Value.CicScore, ruleId = stpRule.Id })
                });
            }
            else
            {
                entity.Status = ApplicationStatus.Submitted;
            }
        }
        else
        {
            entity.Status = ApplicationStatus.Submitted;
        }

        entity.SubmittedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.ApplicationSubmitted,
            EntityType = "Application",
            EntityId = entity.Id
        });
        await _db.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> ApproveAsync(int id, int approverUserId)
    {
        var entity = await _db.LoanApplications
            .Include(a => a.Decisions)
            .FirstOrDefaultAsync(a => a.Id == id);
            
        if (entity is null) return Result.Failure("Application not found.");

        // APR-VAL-001
        if (entity.Status != ApplicationStatus.Submitted)
            return Result.Failure("APR-VAL-001: Only submitted application can be approved.");

        var approverUser = await _db.Users.FindAsync(approverUserId);
        if (approverUser == null) return Result.Failure("Approver not found.");

        var requiredRules = (await _approvalRuleService.GetRequiredRulesForApplicationAsync(id)).ToList();
        
        var approvedDecisions = entity.Decisions?.Where(d => d.Decision == "Approved").ToList() ?? new List<ApplicationDecision>();
        int currentLevel = approvedDecisions.Count + 1;

        var currentRule = requiredRules.FirstOrDefault(r => r.RequiredLevel == currentLevel);
        
        if (currentRule != null)
        {
            if (approverUser.Role != currentRule.ApproverRole && approverUser.Role != Roles.Admin)
            {
                return Result.Failure($"Approval at level {currentLevel} requires role: {currentRule.ApproverRole}. Approver role is {approverUser.Role}.");
            }
        }
        else if (requiredRules.Count > 0 && currentLevel > requiredRules.Max(r => r.RequiredLevel))
        {
            return Result.Failure("Application is already fully approved.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync();

        try
        {
            _db.ApplicationDecisions.Add(new ApplicationDecision
            {
                ApplicationId = id,
                Decision = "Approved",
                DecidedByUserId = approverUserId,
                DecisionReason = currentRule != null ? $"Approved Level {currentLevel}" : "Approved"
            });

            bool isFullyApproved = requiredRules.Count == 0 || currentLevel >= requiredRules.Max(r => r.RequiredLevel);

            if (isFullyApproved)
            {
                entity.Status = ApplicationStatus.Approved;
            }
            
            entity.UpdatedAt = DateTime.UtcNow;

            _db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = approverUserId,
                ActionCode = AuditActions.ApplicationApproved,
                EntityType = "Application",
                EntityId = entity.Id,
                NewValuesJson = AuditHelper.ToJson(new { level = currentLevel, fullyApproved = isFullyApproved })
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return Result.Success();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<Result> RejectAsync(int id, RejectApplicationRequest req, int approverUserId)
    {
        var entity = await _db.LoanApplications.FindAsync(id);
        if (entity is null) return Result.Failure("Application not found.");

        // APR-VAL-001
        if (entity.Status != ApplicationStatus.Submitted)
            return Result.Failure("APR-VAL-001: Only submitted application can be rejected.");

        // APR-VAL-003
        if (string.IsNullOrWhiteSpace(req.Reason))
            return Result.Failure("APR-VAL-003: Rejection reason is required.");

        entity.Status = ApplicationStatus.Rejected;
        entity.UpdatedAt = DateTime.UtcNow;

        _db.ApplicationDecisions.Add(new ApplicationDecision
        {
            ApplicationId = id,
            Decision = "Rejected",
            DecisionReason = req.Reason,
            DecidedByUserId = approverUserId
        });

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = approverUserId,
            ActionCode = AuditActions.ApplicationRejected,
            EntityType = "Application",
            EntityId = entity.Id,
            NewValuesJson = AuditHelper.ToJson(new { reason = req.Reason })
        });

        await _db.SaveChangesAsync();
        return Result.Success();
    }

    /// <summary>Delegates to LoanProductService preview (§APP-005)</summary>
    public Task<Result<InstallmentPreviewDto>> PreviewAsync(InstallmentPreviewRequest request)
        => _productService.PreviewInstallmentAsync(request);

    private static LoanApplicationDto MapToDto(LoanApplication a) => new()
    {
        Id = a.Id,
        CustomerId = a.CustomerId,
        CustomerName = a.Customer?.FullName ?? "",
        CustomerPhone = a.Customer?.Phone ?? "",
        ProductId = a.ProductId,
        ProductName = a.Product?.ProductName ?? "",
        RequestedAmount = a.RequestedAmount,
        RequestedTenorMonths = a.RequestedTenorMonths,
        Purpose = a.Purpose,
        Remarks = a.Remarks,
        Status = a.Status,
        CreatedByName = a.CreatedBy?.FullName ?? "",
        SubmittedAt = a.SubmittedAt,
        CreatedAt = a.CreatedAt,
        LoanId = a.Loan?.Id,
        DecisionHistory = (a.Decisions ?? []).Select(d => new DecisionHistoryDto
        {
            Decision = d.Decision,
            DecisionReason = d.DecisionReason,
            DecidedBy = d.DecidedBy?.FullName ?? "",
            DecidedAt = d.DecidedAt
        }).ToList()
    };
}
