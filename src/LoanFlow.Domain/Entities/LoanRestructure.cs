namespace LoanFlow.Domain.Entities;

/// <summary>Lưu lịch sử cơ cấu lại khoản vay (thay đổi tenor/rate/start date)</summary>
public class LoanRestructure
{
    public int Id { get; set; }
    public int LoanId { get; set; }

    // Giá trị trước khi cơ cấu
    public int OldTenorMonths { get; set; }
    public decimal OldMonthlyInterestRate { get; set; }
    public DateOnly OldMaturityDate { get; set; }

    // Giá trị sau khi cơ cấu
    public int NewTenorMonths { get; set; }
    public decimal NewMonthlyInterestRate { get; set; }
    public DateOnly NewMaturityDate { get; set; }
    public DateOnly RestructureStartDate { get; set; }

    public string ReasonCode { get; set; } = default!;  // "CustomerHardship" | "BusinessFailure" | other
    public string? Note { get; set; }
    public int RequestedByUserId { get; set; }
    public int ApprovedByUserId { get; set; }
    public DateTime RestructuredAt { get; set; } = DateTime.UtcNow;

    public Loan? Loan { get; set; }
    public User? RequestedBy { get; set; }
    public User? ApprovedBy { get; set; }
}
