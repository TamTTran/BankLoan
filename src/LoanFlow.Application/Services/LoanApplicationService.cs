using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.LoanApplication;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public interface ILoanApplicationService
{
    Task<PagedResult<LoanApplicationDto>> GetListAsync(ApplicationFilterRequest filter);
    Task<Result<LoanApplicationDto>> GetByIdAsync(int id);
    Task<Result<LoanApplicationDto>> CreateAsync(CreateApplicationRequest request, int userId, int? branchId);
    Task<Result> SubmitAsync(int id, int userId);
    Task<Result<LoanApplicationDto>> DecideAsync(int id, ApprovalDecisionRequest request, int approverUserId);
    Task<Result> CancelAsync(int id, int userId);
}

public class LoanApplicationService : ILoanApplicationService
{
    private readonly LoanFlowDbContext _db;
    private readonly ILoanProductService _productService;

    public LoanApplicationService(LoanFlowDbContext db, ILoanProductService productService)
    {
        _db = db;
        _productService = productService;
    }

    public async Task<PagedResult<LoanApplicationDto>> GetListAsync(ApplicationFilterRequest filter)
    {
        var query = _db.LoanApplications
            .Include(a => a.Customer)
            .Include(a => a.LoanProduct)
            .Include(a => a.CreatedBy)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(a =>
                a.ApplicationNumber.Contains(s) ||
                a.Customer.FullName.ToLower().Contains(s) ||
                a.Customer.PhoneNumber.Contains(s));
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(a => a.Status == filter.Status);
        if (filter.BranchId.HasValue)
            query = query.Where(a => a.BranchId == filter.BranchId);
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
            .Include(x => x.LoanProduct)
            .Include(x => x.CreatedBy)
            .Include(x => x.Decisions).ThenInclude(d => d.DecidedBy)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (a is null) return Result.Failure<LoanApplicationDto>("Hồ sơ vay không tồn tại.");
        return Result.Success(MapToDto(a));
    }

    public async Task<Result<LoanApplicationDto>> CreateAsync(CreateApplicationRequest req, int userId, int? branchId)
    {
        var customer = await _db.Customers.FindAsync(req.CustomerId);
        if (customer is null) return Result.Failure<LoanApplicationDto>("Khách hàng không tồn tại.");

        var preview = await _productService.PreviewInstallmentAsync(new()
        {
            LoanProductId = req.LoanProductId,
            Amount = req.RequestedAmount,
            TenorMonths = req.TenorMonths
        });
        if (!preview.IsSuccess) return Result.Failure<LoanApplicationDto>(preview.Error!);

        var appNumber = await GenerateApplicationNumberAsync();
        var entity = new LoanApplication
        {
            ApplicationNumber = appNumber,
            CustomerId = req.CustomerId,
            LoanProductId = req.LoanProductId,
            BranchId = branchId,
            CreatedByUserId = userId,
            RequestedAmount = req.RequestedAmount,
            TenorMonths = req.TenorMonths,
            EstimatedInstallment = preview.Value!.MonthlyInstallment,
            Purpose = req.Purpose,
            Notes = req.Notes,
            Status = ApplicationStatus.Draft
        };
        _db.LoanApplications.Add(entity);
        await _db.SaveChangesAsync();
        return await GetByIdAsync(entity.Id);
    }

    public async Task<Result> SubmitAsync(int id, int userId)
    {
        var entity = await _db.LoanApplications.FindAsync(id);
        if (entity is null) return Result.Failure("Hồ sơ vay không tồn tại.");
        if (entity.CreatedByUserId != userId) return Result.Failure("Bạn không có quyền nộp hồ sơ này.");
        if (entity.Status != ApplicationStatus.Draft) return Result.Failure($"Không thể nộp hồ sơ ở trạng thái {entity.Status}.");

        entity.Status = ApplicationStatus.Submitted;
        entity.SubmittedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<LoanApplicationDto>> DecideAsync(int id, ApprovalDecisionRequest req, int approverUserId)
    {
        var entity = await _db.LoanApplications.FindAsync(id);
        if (entity is null) return Result.Failure<LoanApplicationDto>("Hồ sơ không tồn tại.");
        if (entity.Status is not (ApplicationStatus.Submitted or ApplicationStatus.UnderReview))
            return Result.Failure<LoanApplicationDto>("Hồ sơ không ở trạng thái chờ duyệt.");

        if (req.Decision == "Rejected" && string.IsNullOrWhiteSpace(req.Remark))
            return Result.Failure<LoanApplicationDto>("Từ chối phải có lý do.");

        entity.Status = req.Decision == "Approved"
            ? ApplicationStatus.Approved
            : req.Decision == "Rejected"
                ? ApplicationStatus.Rejected
                : ApplicationStatus.UnderReview;
        entity.UpdatedAt = DateTime.UtcNow;

        _db.ApplicationDecisions.Add(new ApplicationDecision
        {
            LoanApplicationId = id,
            DecidedByUserId = approverUserId,
            Decision = req.Decision,
            Remark = req.Remark,
            ApprovedAmount = req.ApprovedAmount,
            ApprovedTenorMonths = req.ApprovedTenorMonths
        });

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = approverUserId,
            Action = req.Decision.ToUpper(),
            EntityType = "LoanApplication",
            EntityId = id.ToString(),
            NewValues = $"{{\"decision\":\"{req.Decision}\",\"remark\":\"{req.Remark}\"}}"
        });

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<Result> CancelAsync(int id, int userId)
    {
        var entity = await _db.LoanApplications.FindAsync(id);
        if (entity is null) return Result.Failure("Hồ sơ không tồn tại.");
        if (entity.Status is ApplicationStatus.Approved or ApplicationStatus.Rejected)
            return Result.Failure("Không thể hủy hồ sơ đã có quyết định.");

        entity.Status = ApplicationStatus.Cancelled;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    private async Task<string> GenerateApplicationNumberAsync()
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        var prefix = $"APP-{today}-";
        var last = await _db.LoanApplications
            .Where(a => a.ApplicationNumber.StartsWith(prefix))
            .OrderByDescending(a => a.ApplicationNumber)
            .Select(a => a.ApplicationNumber)
            .FirstOrDefaultAsync();

        int seq = 1;
        if (last is not null)
        {
            var parts = last.Split('-');
            if (parts.Length >= 3 && int.TryParse(parts[^1], out var n)) seq = n + 1;
        }
        return $"{prefix}{seq:D4}";
    }

    private static LoanApplicationDto MapToDto(LoanApplication a) => new()
    {
        Id = a.Id, ApplicationNumber = a.ApplicationNumber,
        CustomerId = a.CustomerId, CustomerName = a.Customer?.FullName ?? "",
        CustomerPhone = a.Customer?.PhoneNumber ?? "",
        LoanProductId = a.LoanProductId, ProductName = a.LoanProduct?.Name ?? "",
        RequestedAmount = a.RequestedAmount, TenorMonths = a.TenorMonths,
        EstimatedInstallment = a.EstimatedInstallment, Purpose = a.Purpose,
        Status = a.Status, Notes = a.Notes,
        CreatedByName = a.CreatedBy?.FullName ?? "",
        CreatedAt = a.CreatedAt, SubmittedAt = a.SubmittedAt,
        DecisionHistory = (a.Decisions ?? []).Select(d => new DecisionHistoryDto
        {
            Decision = d.Decision, Remark = d.Remark,
            DecidedBy = d.DecidedBy?.FullName ?? "",
            DecidedAt = d.DecidedAt,
            ApprovedAmount = d.ApprovedAmount,
            ApprovedTenorMonths = d.ApprovedTenorMonths
        }).ToList()
    };
}
