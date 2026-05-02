using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class LiquidityReportService : ILiquidityReportService
{
    private readonly LoanFlowDbContext _db;

    public LiquidityReportService(LoanFlowDbContext db)
    {
        _db = db;
    }

    public async Task<LiquidityReportDto> GenerateReportAsync(int year, int month)
    {
        // 1. LCR Calculation
        // HQLA = Cash + Govt Bonds (Mocked for now)
        decimal hqla = 150000000000m; // 150 tỷ VND
        
        // Net Cash Outflows 30 days = Expected Outflows - Expected Inflows
        // Inflows from loan repayments in next 30 days
        var today = new DateOnly(year, month, 1).ToDateTime(TimeOnly.MinValue);
        var next30Days = today.AddDays(30);
        
        var loanInflows = await _db.RepaymentSchedules
            .Where(s => s.DueDate >= DateOnly.FromDateTime(today) && s.DueDate <= DateOnly.FromDateTime(next30Days) && s.Status == Domain.Constants.InstallmentStatus.Unpaid)
            .SumAsync(s => s.PrincipalDue + s.InterestDue);

        // Outflows from deposits maturing in 30 days (Mocked)
        decimal depositOutflows = 100000000000m; // 100 tỷ VND
        
        decimal netOutflows30Days = depositOutflows - loanInflows;
        if (netOutflows30Days < 0) netOutflows30Days = 0;

        // 2. NSFR Calculation
        // ASF (Available Stable Funding) = Capital + Long-term deposits (Mocked)
        decimal asf = 600000000000m; // 600 tỷ VND

        // RSF (Required Stable Funding) = Loans > 1 year
        var longTermLoans = await _db.Loans
            .Where(l => l.Status == Domain.Constants.LoanStatus.Active && l.TenorMonths > 12)
            .SumAsync(l => l.OutstandingAmount);

        decimal rsf = longTermLoans + 100000000000m; // Add mock 100 tỷ for other assets

        return new LiquidityReportDto
        {
            Year = year,
            Month = month,
            HighQualityLiquidAssets = hqla,
            TotalNetCashOutflows30Days = netOutflows30Days,
            AvailableStableFunding = asf,
            RequiredStableFunding = rsf
        };
    }
}
