namespace LoanFlow.Domain.Entities;

public class AmlRiskAssessment
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    
    /// <summary>Low | Medium | High</summary>
    public string RiskLevel { get; set; } = "Low";
    
    /// <summary>Politically Exposed Person: None | Domestic | Foreign</summary>
    public string PepStatus { get; set; } = "None";
    
    public bool HasSanctionHit { get; set; }
    public string? SanctionDetails { get; set; }
    
    public DateTime LastScreeningDate { get; set; } = DateTime.UtcNow;
    public DateTime NextReviewDate { get; set; }
    
    public int AssessedByUserId { get; set; }
    public string? Remarks { get; set; }

    public Customer Customer { get; set; } = default!;
}
