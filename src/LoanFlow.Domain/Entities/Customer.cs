namespace LoanFlow.Domain.Entities;

public class Customer
{
    public int Id { get; set; }
    public string CustomerCode { get; set; } = default!; // Auto-generated: CUS-YYYYMMDD-XXXXX
    public string FullName { get; set; } = default!;
    public string NationalId { get; set; } = default!; // CCCD / CMND - unique
    public string PhoneNumber { get; set; } = default!; // unique
    public string? Email { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? Occupation { get; set; }
    public string? MonthlyIncome { get; set; }
    public int? BranchId { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Branch? Branch { get; set; }
    public User? CreatedBy { get; set; }
    public ICollection<CustomerDocument> Documents { get; set; } = new List<CustomerDocument>();
    public ICollection<LoanApplication> LoanApplications { get; set; } = new List<LoanApplication>();
}
