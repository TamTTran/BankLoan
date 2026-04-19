namespace LoanFlow.Domain.Entities;

/// <summary>
/// Loan document uploaded for verification (CMND, contract, bank statement, etc.)
/// </summary>
public class LoanDocument
{
    public int Id { get; set; }
    public int? LoanId { get; set; }
    public int? CustomerId { get; set; }
    public int? ApplicationId { get; set; }

    /// <summary>IdCard | Contract | BankStatement | PaySlip | CollateralPhoto | Other</summary>
    public string DocumentType { get; set; } = default!;

    public string FileName { get; set; } = default!;
    public string FilePath { get; set; } = default!;      // relative path in storage
    public long FileSize { get; set; }                     // bytes
    public string? MimeType { get; set; }

    /// <summary>Uploaded | Verified | Rejected | Expired</summary>
    public string Status { get; set; } = "Uploaded";

    public string? VerificationNotes { get; set; }
    public int UploadedByUserId { get; set; }
    public int? VerifiedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAt { get; set; }

    public Loan? Loan { get; set; }
    public Customer? Customer { get; set; }
    public User UploadedBy { get; set; } = default!;
    public User? VerifiedBy { get; set; }
}
