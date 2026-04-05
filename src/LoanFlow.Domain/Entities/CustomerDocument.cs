namespace LoanFlow.Domain.Entities;

public class CustomerDocument
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string DocumentType { get; set; } = default!; // CCCD_FRONT | CCCD_BACK | HOUSEHOLD | SALARY_SLIP | OTHER
    public string FileName { get; set; } = default!;
    public string FilePath { get; set; } = default!;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public int UploadedByUserId { get; set; }

    public Customer Customer { get; set; } = default!;
    public User UploadedBy { get; set; } = default!;
}
