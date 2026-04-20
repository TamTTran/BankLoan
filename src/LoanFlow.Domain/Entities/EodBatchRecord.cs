namespace LoanFlow.Domain.Entities;

public class EodBatchRecord
{
    public int Id { get; set; }
    public DateOnly BusinessDate { get; set; }
    
    // Status can be: Pending, Running, Completed, Failed
    public string Status { get; set; } = "Pending";
    
    // Step 1: Recon, Step 2: Accrual, Step 3: Overdue, Step 4: Classification, Step 5: Notifications
    public int LastCompletedStep { get; set; } = 0; 
    
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
