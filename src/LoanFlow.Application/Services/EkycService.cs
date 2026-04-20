using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LoanFlow.Application.Services;

// ── eKYC Provider Interface ─────────────────────────────────────────────────
public class EkycResultDto
{
    public string? FullNameExtracted { get; set; }
    public decimal FaceMatchScore { get; set; }
    public decimal OcrConfidence { get; set; }
    public decimal LivenessScore { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RawJson { get; set; }
}

public interface IEkycProvider
{
    string ProviderName { get; }
    Task<EkycResultDto> VerifyAsync(string idNumber, string idType, string fullName);
}

// ── eKYC Service DTOs ───────────────────────────────────────────────────────
public class EkycVerificationDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateTime VerificationDate { get; set; }
    public string IdType { get; set; } = "";
    public string? FullNameExtracted { get; set; }
    public decimal? FaceMatchScore { get; set; }
    public decimal? OcrConfidence { get; set; }
    public decimal? LivenessScore { get; set; }
    public string Status { get; set; } = "";
    public string Provider { get; set; } = "";
}

// ── Interface ───────────────────────────────────────────────────────────────
public interface IEkycService
{
    Task<Result<EkycVerificationDto>> VerifyCustomerAsync(int customerId, string idType, int requestedByUserId);
    Task<List<EkycVerificationDto>> GetHistoryAsync(int customerId);
}

// ── Implementation ──────────────────────────────────────────────────────────
public class EkycService : IEkycService
{
    private readonly LoanFlowDbContext _db;
    private readonly IEkycProvider _provider;

    public EkycService(LoanFlowDbContext db, IEkycProvider provider)
    {
        _db = db;
        _provider = provider;
    }

    public async Task<Result<EkycVerificationDto>> VerifyCustomerAsync(int customerId, string idType, int requestedByUserId)
    {
        var customer = await _db.Customers.FindAsync(customerId);
        if (customer == null) return Result.Failure<EkycVerificationDto>("Customer not found.");

        var verification = new EkycVerification
        {
            CustomerId = customerId,
            IdType = idType,
            IdNumber = customer.IdNumber,
            RequestedByUserId = requestedByUserId,
            Provider = _provider.ProviderName,
            Status = "Pending"
        };

        try
        {
            var result = await _provider.VerifyAsync(customer.IdNumber, idType, customer.FullName);

            if (result.IsSuccess)
            {
                verification.FullNameExtracted = result.FullNameExtracted;
                verification.FaceMatchScore = result.FaceMatchScore;
                verification.OcrConfidence = result.OcrConfidence;
                verification.LivenessScore = result.LivenessScore;
                verification.RawResponse = result.RawJson;

                // Auto-determine status based on scores (Digital Finance 2345/QĐ-NHNN policy)
                if (result.FaceMatchScore >= 80 && result.OcrConfidence >= 85 && result.LivenessScore >= 95)
                {
                    verification.Status = "Verified";
                    customer.EkycStatus = "Verified";
                    customer.EkycVerifiedAt = DateTime.UtcNow;

                    // Sync OCR data back to customer master to ensure truth matching CCCD
                    if (!string.IsNullOrWhiteSpace(result.FullNameExtracted) && customer.FullName != result.FullNameExtracted)
                    {
                        customer.FullName = result.FullNameExtracted;
                    }
                }
                else if (result.FaceMatchScore >= 60)
                {
                    verification.Status = "ManualReview";
                    customer.EkycStatus = "ManualReview";
                }
                else
                {
                    verification.Status = "Rejected";
                    customer.EkycStatus = "Rejected";
                }
            }
            else
            {
                verification.Status = "Rejected";
                customer.EkycStatus = "Rejected";
                verification.RawResponse = JsonSerializer.Serialize(new { error = result.ErrorMessage });
            }
            _db.Customers.Update(customer);
        }
        catch (Exception ex)
        {
            verification.Status = "Rejected";
            verification.RawResponse = JsonSerializer.Serialize(new { error = ex.Message });
        }

        _db.Set<EkycVerification>().Add(verification);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = requestedByUserId,
            ActionCode = "EKYC_VERIFICATION",
            EntityType = "EkycVerification",
            EntityId = 0,
            NewValuesJson = AuditHelper.ToJson(new { customerId, idType, status = verification.Status })
        });

        await _db.SaveChangesAsync();

        return Result.Success(new EkycVerificationDto
        {
            Id = verification.Id, CustomerId = verification.CustomerId,
            VerificationDate = verification.VerificationDate,
            IdType = verification.IdType,
            FullNameExtracted = verification.FullNameExtracted,
            FaceMatchScore = verification.FaceMatchScore,
            OcrConfidence = verification.OcrConfidence,
            LivenessScore = verification.LivenessScore,
            Status = verification.Status, Provider = verification.Provider
        });
    }

    public async Task<List<EkycVerificationDto>> GetHistoryAsync(int customerId)
    {
        return await _db.Set<EkycVerification>()
            .Where(e => e.CustomerId == customerId)
            .OrderByDescending(e => e.VerificationDate)
            .Select(e => new EkycVerificationDto
            {
                Id = e.Id, CustomerId = e.CustomerId,
                VerificationDate = e.VerificationDate,
                IdType = e.IdType,
                FullNameExtracted = e.FullNameExtracted,
                FaceMatchScore = e.FaceMatchScore,
                OcrConfidence = e.OcrConfidence,
                LivenessScore = e.LivenessScore,
                Status = e.Status, Provider = e.Provider
            })
            .ToListAsync();
    }
}
