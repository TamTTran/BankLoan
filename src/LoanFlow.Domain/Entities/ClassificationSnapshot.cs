namespace LoanFlow.Domain.Entities;

/// <summary>
/// Daily snapshot of loan classification for NPL migration matrix reporting.
/// One row per loan per business day (written by DailyClassificationBackgroundService).
/// </summary>
public class ClassificationSnapshot
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public DateOnly SnapshotDate { get; set; }
    
    /// <summary>1-5 per NHNN groups</summary>
    public int Classification { get; set; }
    
    public decimal OutstandingAmount { get; set; }
    public decimal ProvisionAmount { get; set; }
    public int Dpd { get; set; }

    // IFRS 9 Data
    public int Ifrs9Stage { get; set; } = 1;
    public decimal Ifrs9EclAmount { get; set; }

    public Loan Loan { get; set; } = default!;
}
