namespace LoanFlow.Domain.Entities;

public class EContract
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public string ContractNumber { get; set; } = default!;
    
    // Electronic Signature Details
    // Electronic Signature Details
    public string PdfContentHash { get; set; } = default!; // SHA-256
    public string DigitalSignature { get; set; } = default!; // RSA Signature from CA
    public string ClientIpAddress { get; set; } = default!;
    
    // Archive
    public string? PdfStoragePath { get; set; } // Path to stored PDF

    public DateTime SignedAt { get; set; } = DateTime.UtcNow;

    public Loan Loan { get; set; } = default!;
}
