namespace LoanFlow.Application.Services;

public class IndustryConcentrationDto
{
    public string IndustryCode { get; set; } = default!;
    public decimal TotalExposure { get; set; }
    public decimal PortfolioShare { get; set; } // %
    public decimal LimitPercentage { get; set; } = 15.0m;
    public bool IsBreached => PortfolioShare > LimitPercentage;
}

public class SingleBorrowerConcentrationDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = default!;
    public decimal TotalExposure { get; set; }
    public decimal EquityShare { get; set; } // % of Bank's Equity
    public decimal LimitPercentage { get; set; } = 25.0m;
    public bool IsBreached => EquityShare > LimitPercentage;
}

public class ConcentrationReportDto
{
    public DateOnly AsOfDate { get; set; }
    public decimal TotalPortfolioExposure { get; set; }
    public decimal BankEquity { get; set; }
    public List<IndustryConcentrationDto> IndustryConcentrations { get; set; } = [];
    public List<SingleBorrowerConcentrationDto> TopBorrowers { get; set; } = [];
}
