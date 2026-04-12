namespace LoanFlow.Domain.Entities;

public class CustomerCareNote
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string InteractionType { get; set; } = default!;   // Call | Email | Zalo | SMS | Meeting | Other
    public DateTime InteractionDate { get; set; }
    public string Note { get; set; } = default!;              // ghi chú chi tiết
    public DateTime? NextFollowUpDate { get; set; }           // hẹn lịch liên hệ lại
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Customer Customer { get; set; } = default!;
    public User CreatedBy { get; set; } = default!;
}
