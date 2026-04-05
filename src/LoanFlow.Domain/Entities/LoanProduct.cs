using System.Text.Json;

namespace LoanFlow.Domain.Entities;

public class LoanProduct
{
    public int Id { get; set; }
    public string ProductName { get; set; } = default!;     // unique
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public string AllowedTenorsJson { get; set; } = "[]";   // JSON array e.g. [6,12,18]
    public decimal MonthlyInterestRate { get; set; }        // decimal e.g. 0.015 = 1.5%
    public decimal ProcessingFee { get; set; }              // fixed fee amount
    public string RepaymentFrequency { get; set; } = "Monthly";
    public string Status { get; set; } = "Active";          // Active | Inactive
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<LoanApplication> LoanApplications { get; set; } = new List<LoanApplication>();

    // Helper: parse allowed tenors from JSON
    public List<int> GetAllowedTenors() =>
        JsonSerializer.Deserialize<List<int>>(AllowedTenorsJson) ?? [];
}
