using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LoanFlow.Application.Services;

// ── CIC Provider Interface (Strategy Pattern) ───────────────────────────────
public class CicInquiryResultDto
{
    public int? Score { get; set; }
    public string? Grade { get; set; }
    public decimal? TotalBorrowing { get; set; }
    public int? MaxDpd { get; set; }
    public string? RawJson { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface ICicProvider
{
    string ProviderName { get; }
    Task<CicInquiryResultDto> InquireAsync(string idNumber, string fullName);
}

// ── CIC Service DTOs ────────────────────────────────────────────────────────
public class CicInquiryDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateTime InquiryDate { get; set; }
    public int? CicScore { get; set; }
    public string? CicGrade { get; set; }
    public decimal? TotalBorrowingAtCic { get; set; }
    public int? MaxDpdAtCic { get; set; }
    public string Status { get; set; } = "";
    public string Provider { get; set; } = "";
}

// ── Interface ───────────────────────────────────────────────────────────────
public interface ICicService
{
    Task<Result<CicInquiryDto>> InquireCicAsync(int customerId, int requestedByUserId);
    Task<List<CicInquiryDto>> GetHistoryAsync(int customerId);
}

// ── Implementation ──────────────────────────────────────────────────────────
public class CicService : ICicService
{
    private readonly LoanFlowDbContext _db;
    private readonly ICicProvider _provider;

    public CicService(LoanFlowDbContext db, ICicProvider provider)
    {
        _db = db;
        _provider = provider;
    }

    public async Task<Result<CicInquiryDto>> InquireCicAsync(int customerId, int requestedByUserId)
    {
        var customer = await _db.Customers.FindAsync(customerId);
        if (customer == null) return Result.Failure<CicInquiryDto>("Customer not found.");

        var inquiry = new CicInquiry
        {
            CustomerId = customerId,
            RequestedByUserId = requestedByUserId,
            Provider = _provider.ProviderName,
            Status = "Pending"
        };

        try
        {
            var result = await _provider.InquireAsync(customer.IdNumber, customer.FullName);

            if (result.IsSuccess)
            {
                inquiry.Status = "Success";
                inquiry.CicScore = result.Score;
                inquiry.CicGrade = result.Grade;
                inquiry.TotalBorrowingAtCic = result.TotalBorrowing;
                inquiry.MaxDpdAtCic = result.MaxDpd;
                inquiry.RawResponse = result.RawJson;
            }
            else
            {
                inquiry.Status = "Failed";
                inquiry.RawResponse = JsonSerializer.Serialize(new { error = result.ErrorMessage });
            }
        }
        catch (TaskCanceledException)
        {
            inquiry.Status = "Timeout";
        }
        catch (Exception ex)
        {
            inquiry.Status = "Failed";
            inquiry.RawResponse = JsonSerializer.Serialize(new { error = ex.Message });
        }

        _db.Set<CicInquiry>().Add(inquiry);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = requestedByUserId,
            ActionCode = "CIC_INQUIRY",
            EntityType = "CicInquiry",
            EntityId = 0,
            NewValuesJson = AuditHelper.ToJson(new { customerId, provider = _provider.ProviderName, status = inquiry.Status })
        });

        await _db.SaveChangesAsync();

        return Result.Success(new CicInquiryDto
        {
            Id = inquiry.Id, CustomerId = inquiry.CustomerId,
            InquiryDate = inquiry.InquiryDate,
            CicScore = inquiry.CicScore, CicGrade = inquiry.CicGrade,
            TotalBorrowingAtCic = inquiry.TotalBorrowingAtCic,
            MaxDpdAtCic = inquiry.MaxDpdAtCic,
            Status = inquiry.Status, Provider = inquiry.Provider
        });
    }

    public async Task<List<CicInquiryDto>> GetHistoryAsync(int customerId)
    {
        return await _db.Set<CicInquiry>()
            .Where(c => c.CustomerId == customerId)
            .OrderByDescending(c => c.InquiryDate)
            .Select(c => new CicInquiryDto
            {
                Id = c.Id, CustomerId = c.CustomerId,
                InquiryDate = c.InquiryDate,
                CicScore = c.CicScore, CicGrade = c.CicGrade,
                TotalBorrowingAtCic = c.TotalBorrowingAtCic,
                MaxDpdAtCic = c.MaxDpdAtCic,
                Status = c.Status, Provider = c.Provider
            })
            .ToListAsync();
    }
}
