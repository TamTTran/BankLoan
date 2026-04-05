namespace LoanFlow.Domain.Entities;

public class Customer
{
    public int Id { get; set; }
    public string FullName { get; set; } = default!;
    public string Phone { get; set; } = default!;           // unique
    public string IdNumber { get; set; } = default!;        // unique — CCCD/CMND
    public string Address { get; set; } = default!;
    public DateTime DateOfBirth { get; set; }
    public string Status { get; set; } = "Active";          // Active | Inactive
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? CreatedBy { get; set; }
    public ICollection<LoanApplication> LoanApplications { get; set; } = new List<LoanApplication>();
}
