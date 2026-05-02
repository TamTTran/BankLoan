namespace LoanFlow.Application.Services;

public class StressTestScenario
{
    public string ScenarioName { get; set; } = default!;
    public decimal InterestRateShockParams { get; set; }
    public decimal GdpContractionParams { get; set; }
    public decimal PdMultiplier { get; set; }
    public decimal LgdMultiplier { get; set; }
}

public class StressTestResultDto
{
    public string ScenarioName { get; set; } = default!;
    public decimal BaseExpectedLoss { get; set; }
    public decimal StressedExpectedLoss { get; set; }
    public decimal ImpactAmount { get; set; }
    public decimal ImpactPercentage { get; set; }
    public decimal StressedPortfolioNplRatio { get; set; }
}
