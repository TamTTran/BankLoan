namespace LoanFlow.Application.Services;

public class FinancialReportDto
{
    public DateOnly AsOfDate { get; set; }
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal TotalEquity { get; set; }
    public decimal NetIncome { get; set; }
    public List<AccountBalanceDto> Details { get; set; } = [];
}

public class AccountBalanceDto
{
    public string AccountCode { get; set; } = default!;
    public string AccountName { get; set; } = default!;
    public string AccountType { get; set; } = default!;
    public decimal Balance { get; set; }
}
