namespace LoanFlow.Domain.Entities;

/// <summary>Lưu lịch sử điều chỉnh lịch trả nợ (đổi ngày due, không đổi tenor/rate)</summary>
public class LoanReschedule
{
    public int Id { get; set; }
    public int LoanId { get; set; }

    public int OldPaymentDayOfMonth { get; set; }   // ngày trong tháng trước (e.g. 15)
    public int NewPaymentDayOfMonth { get; set; }   // ngày trong tháng mới  (e.g. 25)
    public DateOnly EffectiveFrom { get; set; }

    public string ReasonCode { get; set; } = default!;
    public string? Note { get; set; }
    public int RequestedByUserId { get; set; }
    public int ApprovedByUserId { get; set; }
    public DateTime RescheduledAt { get; set; } = DateTime.UtcNow;

    public Loan? Loan { get; set; }
    public User? RequestedBy { get; set; }
    public User? ApprovedBy { get; set; }
}
