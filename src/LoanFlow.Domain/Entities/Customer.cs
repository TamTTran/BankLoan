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
    public string EkycStatus { get; set; } = "Pending";     // Pending | Verified | Rejected | ManualReview
    public DateTime? EkycVerifiedAt { get; set; }
    public int? CreditScore { get; set; }
    public string? CreditStatus { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankCode { get; set; }
    public int CreatedByUserId { get; set; }
    public int AssignedToUserId { get; set; }             // nhân viên đang phụ trách
    public int? BranchId { get; set; }                    // chi nhánh quản lý
    public decimal? MonthlyIncome { get; set; }           // VND — phục vụ tính toán DTI
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? CreatedBy { get; set; }
    public User? AssignedToUser { get; set; }
    public ICollection<LoanApplication> LoanApplications { get; set; } = new List<LoanApplication>();
    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
    public ICollection<Collateral> Collaterals { get; set; } = new List<Collateral>();
    public ICollection<CreditCheck> CreditChecks { get; set; } = new List<CreditCheck>();
    public ICollection<CustomerCareNote> CareNotes { get; set; } = new List<CustomerCareNote>();
}
