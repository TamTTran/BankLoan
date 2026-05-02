namespace LoanFlow.Application.Services;

public class ExpectedLossDto
{
    public int LoanId { get; set; }
    public decimal ExposureAtDefault { get; set; }
    public decimal ProbabilityOfDefault { get; set; } // 0 to 1
    public decimal LossGivenDefault { get; set; }     // 0 to 1
    public decimal ExpectedLoss { get; set; }
    public int CurrentDpd { get; set; }
}

public class PortfolioExpectedLossDto
{
    public DateOnly AsOfDate { get; set; }
    public decimal TotalExposure { get; set; }
    public decimal TotalExpectedLoss { get; set; }
    public decimal PortfolioExpectedLossRate { get; set; }
    public List<ExpectedLossDto> LoanDetails { get; set; } = [];
}
