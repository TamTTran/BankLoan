namespace LoanFlow.Domain.Entities;

/// <summary>
/// Records a CIC (Credit Information Center) inquiry for a customer.
/// </summary>
public class CicInquiry
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateTime InquiryDate { get; set; } = DateTime.UtcNow;

    // CIC Result
    public int? CicScore { get; set; }
    public string? CicGrade { get; set; }
    public decimal? TotalBorrowingAtCic { get; set; }
    public int? MaxDpdAtCic { get; set; }

    /// <summary>Raw JSON response blob from the CIC provider</summary>
    public string? RawResponse { get; set; }

    /// <summary>Pending | Success | Failed | Timeout</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>Provider name: NAPAS | PCB | MOCK</summary>
    public string Provider { get; set; } = "MOCK";

    public int RequestedByUserId { get; set; }

    public Customer Customer { get; set; } = default!;
    public User RequestedBy { get; set; } = default!;
}
