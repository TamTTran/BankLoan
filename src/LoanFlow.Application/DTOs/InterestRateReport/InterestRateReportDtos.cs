namespace LoanFlow.Application.Services;

public class InterestRateReportDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public decimal OverallAverageInterestRate { get; set; }
    public List<InterestRateByProductDto> ByProduct { get; set; } = [];
    public List<InterestRateByTenorDto> ByTenor { get; set; } = [];
}

public class InterestRateByProductDto
{
    public string ProductType { get; set; } = "";
    public decimal AverageInterestRate { get; set; }
    public decimal TotalOutstanding { get; set; }
}

public class InterestRateByTenorDto
{
    public string TenorBucket { get; set; } = ""; // e.g. "0-6 months", "6-12 months", ">12 months"
    public decimal AverageInterestRate { get; set; }
    public decimal TotalOutstanding { get; set; }
}
