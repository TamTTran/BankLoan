using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LoanFlow.Application.Services;

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
