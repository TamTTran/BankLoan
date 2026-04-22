using System;

namespace LoanFlow.Domain.Entities;

public class EscalationRule
{
    public int Id { get; set; }
    public string DpdCondition { get; set; } = ">"; // ">", ">=", "=="
    public int DpdThreshold { get; set; }
    public string Bucket { get; set; } = default!; // "SMA", "Substandard", "Doubtful", "Loss", "NPL", etc.
    public string ActionToTake { get; set; } = default!; // "TransferToLegal", "WriteOffWarning", "FieldVisit"
    public string Status { get; set; } = "Active"; // Active, Inactive
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
