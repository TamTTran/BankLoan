using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class LoanTopUpDto
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public string LoanAccountNo { get; set; } = "";
    public decimal TopUpAmount { get; set; }
    public decimal PreviousOutstanding { get; set; }
    public decimal NewOutstanding { get; set; }
    public string Status { get; set; } = "";
    public DateTime RequestedAt { get; set; }
}

public class TopUpRequest
{
    public decimal TopUpAmount { get; set; }
    public int NewTenorMonths { get; set; }
    public string? Remarks { get; set; }
}

public interface ILoanTopUpService
{
    Task<Result<LoanTopUpDto>> RequestTopUpAsync(int loanId, TopUpRequest req, int userId);
    Task<Result> ApproveTopUpAsync(int topUpId, int userId);
    Task<Result> RejectTopUpAsync(int topUpId, int userId);
    Task<List<LoanTopUpDto>> GetTopUpsAsync(int? loanId);
}

public class LoanTopUpService : ILoanTopUpService
{
    private readonly LoanFlowDbContext _db;
    private readonly IExposureLimitService _exposureService;

    public LoanTopUpService(LoanFlowDbContext db, IExposureLimitService exposureService)
    {
        _db = db;
        _exposureService = exposureService;
    }

    public async Task<Result<LoanTopUpDto>> RequestTopUpAsync(int loanId, TopUpRequest req, int userId)
    {
        var loan = await _db.Loans.FindAsync(loanId);
        if (loan == null) return Result.Failure<LoanTopUpDto>("Loan not found.");
        if (loan.Status != LoanStatus.Active)
            return Result.Failure<LoanTopUpDto>("Top-up is only allowed for active loans.");

        // Check exposure limit
        var exposureCheck = await _exposureService.CheckExposureAsync(loan.CustomerId, req.TopUpAmount);
        if (!exposureCheck.IsSuccess)
            return Result.Failure<LoanTopUpDto>(exposureCheck.Error!);

        var topUp = new LoanTopUp
        {
            LoanId = loanId,
            TopUpAmount = req.TopUpAmount,
            PreviousOutstanding = loan.OutstandingAmount,
            NewOutstanding = loan.OutstandingAmount + req.TopUpAmount,
            NewTenorMonths = req.NewTenorMonths,
            NewMonthlyRate = loan.MonthlyInterestRate,
            RequestedByUserId = userId,
            Remarks = req.Remarks
        };

        _db.Set<LoanTopUp>().Add(topUp);
        await _db.SaveChangesAsync();

        return Result.Success(new LoanTopUpDto
        {
            Id = topUp.Id, LoanId = loanId, LoanAccountNo = loan.LoanAccountNo,
            TopUpAmount = topUp.TopUpAmount, PreviousOutstanding = topUp.PreviousOutstanding,
            NewOutstanding = topUp.NewOutstanding, Status = topUp.Status, RequestedAt = topUp.RequestedAt
        });
    }

    public async Task<Result> ApproveTopUpAsync(int topUpId, int userId)
    {
        var topUp = await _db.Set<LoanTopUp>().Include(t => t.Loan).FirstOrDefaultAsync(t => t.Id == topUpId);
        if (topUp == null) return Result.Failure("Top-up request not found.");
        if (topUp.Status != "Pending") return Result.Failure("Top-up is not pending.");
        if (topUp.RequestedByUserId == userId) return Result.Failure("Self-approval not allowed.");

        topUp.Status = "Approved";
        topUp.ApprovedByUserId = userId;
        topUp.ApprovedAt = DateTime.UtcNow;

        // Update loan
        topUp.Loan.OutstandingAmount += topUp.TopUpAmount;
        topUp.Loan.ApprovedAmount += topUp.TopUpAmount;
        topUp.Loan.TenorMonths = topUp.NewTenorMonths;
        topUp.Loan.UpdatedAt = DateTime.UtcNow;

        topUp.Status = "Disbursed";
        topUp.DisbursedAt = DateTime.UtcNow;

        // Refresh exposure
        await _exposureService.RefreshExposureAsync(topUp.Loan.CustomerId);
        await _db.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> RejectTopUpAsync(int topUpId, int userId)
    {
        var topUp = await _db.Set<LoanTopUp>().FindAsync(topUpId);
        if (topUp == null) return Result.Failure("Top-up request not found.");
        if (topUp.Status != "Pending") return Result.Failure("Top-up is not pending.");

        topUp.Status = "Rejected";
        topUp.ApprovedByUserId = userId;
        topUp.ApprovedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<List<LoanTopUpDto>> GetTopUpsAsync(int? loanId)
    {
        var query = _db.Set<LoanTopUp>().Include(t => t.Loan).AsQueryable();
        if (loanId.HasValue) query = query.Where(t => t.LoanId == loanId.Value);

        return await query.OrderByDescending(t => t.RequestedAt)
            .Select(t => new LoanTopUpDto
            {
                Id = t.Id, LoanId = t.LoanId, LoanAccountNo = t.Loan.LoanAccountNo,
                TopUpAmount = t.TopUpAmount, PreviousOutstanding = t.PreviousOutstanding,
                NewOutstanding = t.NewOutstanding, Status = t.Status, RequestedAt = t.RequestedAt
            }).ToListAsync();
    }
}
