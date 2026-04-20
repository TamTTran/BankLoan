namespace LoanFlow.Domain.Entities;

/// <summary>
/// Records an eKYC verification result for a customer.
/// </summary>
public class EkycVerification
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateTime VerificationDate { get; set; } = DateTime.UtcNow;

    /// <summary>CMND | CCCD | Passport</summary>
    public string IdType { get; set; } = "CCCD";
    public string IdNumber { get; set; } = default!;
    public string? FullNameExtracted { get; set; }

    /// <summary>Face match confidence 0-100</summary>
    public decimal? FaceMatchScore { get; set; }

    /// <summary>OCR extraction confidence 0-100</summary>
    public decimal? OcrConfidence { get; set; }

    /// <summary>Liveness detection confidence 0-100</summary>
    public decimal? LivenessScore { get; set; }

    /// <summary>Pending | Verified | Rejected | ManualReview</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>Provider: VNPT_EKYC | FPT_EKYC | MOCK</summary>
    public string Provider { get; set; } = "MOCK";

    /// <summary>Raw JSON response from the eKYC provider</summary>
    public string? RawResponse { get; set; }

    public int RequestedByUserId { get; set; }

    public Customer Customer { get; set; } = default!;
    public User RequestedBy { get; set; } = default!;
}
