namespace LoanFlow.Domain.Entities;

public class EContract
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public string ContractNumber { get; set; } = default!;
    
    // Electronic Signature Details
    public string PdfContentHash { get; set; } = default!; // SHA-256
    public string MockDigitalSignature { get; set; } = default!; // Mock RSA Signature
    public string ClientIpAddress { get; set; } = default!;

    public DateTime SignedAt { get; set; } = DateTime.UtcNow;

    public Loan Loan { get; set; } = default!;
}
