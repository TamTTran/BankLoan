namespace LoanFlow.Application.Services;

public class DocumentDto
{
    public int Id { get; set; }
    public int? LoanId { get; set; }
    public int? CustomerId { get; set; }
    public string DocumentType { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public string Status { get; set; } = "";
    public DateTime UploadedAt { get; set; }
    public string? VerificationNotes { get; set; }
}
