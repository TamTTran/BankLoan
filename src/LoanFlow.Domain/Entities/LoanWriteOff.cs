namespace LoanFlow.Domain.Entities;

/// <summary>Ghi nhận xóa nợ (write-off) khi khoản vay không còn khả năng thu hồi</summary>
public class LoanWriteOff
{
    public int Id { get; set; }
    public int LoanId { get; set; }

    public decimal WriteOffAmount { get; set; }         // Số tiền xóa nợ
    public decimal UnpaidPrincipal { get; set; }
    public decimal UnpaidInterest { get; set; }
    public decimal UnpaidLateFees { get; set; }

    public string ReasonCode { get; set; } = default!;  // "Uncollectible" | "BankruptCustomer" | "CourtOrder"
    public string? Note { get; set; }
    public DateOnly WriteOffDate { get; set; }
    public int RequestedByUserId { get; set; }
    public int ApprovedByUserId { get; set; }
    public DateTime WrittenOffAt { get; set; } = DateTime.UtcNow;

    public Loan? Loan { get; set; }
    public User? RequestedBy { get; set; }
    public User? ApprovedBy { get; set; }
    public ICollection<LoanRecovery> Recoveries { get; set; } = new List<LoanRecovery>();
}
