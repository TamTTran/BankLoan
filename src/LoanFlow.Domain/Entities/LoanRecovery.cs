namespace LoanFlow.Domain.Entities;

/// <summary>Ghi nhận thu hồi tiền sau khi khoản vay đã bị write-off</summary>
public class LoanRecovery
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public int WriteOffId { get; set; }

    public decimal RecoveredAmount { get; set; }
    public DateOnly RecoveryDate { get; set; }

    // "CashPayment" | "CollateralLiquidation" | "CourtOrder" | "ThirdPartyCollection"
    public string RecoveryMethod { get; set; } = default!;
    public string? Note { get; set; }
    public int RecordedByUserId { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public Loan? Loan { get; set; }
    public LoanWriteOff? WriteOff { get; set; }
    public User? RecordedBy { get; set; }
}
