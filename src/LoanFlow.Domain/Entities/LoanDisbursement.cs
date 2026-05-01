namespace LoanFlow.Domain.Entities;

public class LoanDisbursement
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public decimal DisbursedAmount { get; set; }            // must equal approved amount
    public DateOnly DisbursementDate { get; set; }
    public int RecordedByUserId { get; set; }
    public string DisbursementMethod { get; set; } = "Cash";    // Cash, BankTransfer, Failed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── NAPAS Gateway Tracking ───────────────────────────────────────────────
    /// <summary>
    /// Trạng thái giao dịch NAPAS: Pending (chờ xác nhận) | Completed | Failed | NotApplicable
    /// </summary>
    public string DisbursementStatus { get; set; } = "NotApplicable"; // only set when DisbursementMethod=BankTransfer
    /// <summary>Mã giao dịch NAPAS (referenceId do hệ thống tạo, dùng để trace)</summary>
    public string? NapasReferenceId { get; set; }
    /// <summary>Mã giao dịch NAPAS trả về (transactionId từ NAPAS)</summary>
    public string? NapasTransactionId { get; set; }
    /// <summary>Thời điểm NAPAS xác nhận thành công / thất bại</summary>
    public DateTime? GatewayConfirmedAt { get; set; }
    /// <summary>Lý do thất bại từ NAPAS (nếu có)</summary>
    public string? GatewayErrorMessage { get; set; }

    public Loan Loan { get; set; } = default!;
    public User RecordedBy { get; set; } = default!;
}
