using System;

namespace LoanFlow.Domain.Entities;

public class LoanAdjustment
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    
    // "Principal" | "Interest" | "LateFee" | "Fee"
    public string AdjustmentType { get; set; } = default!; 
    
    public decimal Amount { get; set; } // + or -
    
    public string ReasonCode { get; set; } = default!;
    
    public string? Note { get; set; }
    
    public int CreatedByUserId { get; set; }
    public int ApprovedByUserId { get; set; }
    
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    public Loan? Loan { get; set; }
    public User? CreatedBy { get; set; }
    public User? ApprovedBy { get; set; }
}
