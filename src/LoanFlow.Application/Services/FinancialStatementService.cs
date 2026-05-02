using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class FinancialStatementService : IFinancialStatementService
{
    private readonly LoanFlowDbContext _db;

    public FinancialStatementService(LoanFlowDbContext db)
    {
        _db = db;
    }

    public async Task<FinancialReportDto> GenerateBalanceSheetAsync(DateOnly asOfDate)
    {
        var accounts = await _db.GLAccounts
            .Where(a => a.AccountType == "Asset" || a.AccountType == "Liability" || a.AccountType == "Equity")
            .ToListAsync();

        var details = accounts.Select(a => new AccountBalanceDto
        {
            AccountCode = a.AccountCode,
            AccountName = a.AccountName,
            AccountType = a.AccountType,
            Balance = a.Balance
        }).ToList();

        return new FinancialReportDto
        {
            AsOfDate = asOfDate,
            TotalAssets = details.Where(d => d.AccountType == "Asset").Sum(d => d.Balance),
            TotalLiabilities = details.Where(d => d.AccountType == "Liability").Sum(d => Math.Abs(d.Balance)),
            TotalEquity = details.Where(d => d.AccountType == "Equity").Sum(d => Math.Abs(d.Balance)),
            Details = details
        };
    }

    public async Task<FinancialReportDto> GenerateProfitAndLossAsync(DateOnly fromDate, DateOnly toDate)
    {
        var accounts = await _db.GLAccounts
            .Where(a => a.AccountType == "Revenue" || a.AccountType == "Expense")
            .ToListAsync();

        // In a real system, we'd query JournalEntryLines between fromDate and toDate to calculate periodic change.
        // For simplicity here, we assume GLAccounts hold the YTD balances.
        
        var details = accounts.Select(a => new AccountBalanceDto
        {
            AccountCode = a.AccountCode,
            AccountName = a.AccountName,
            AccountType = a.AccountType,
            Balance = a.Balance
        }).ToList();

        var totalRevenue = details.Where(d => d.AccountType == "Revenue").Sum(d => Math.Abs(d.Balance));
        var totalExpense = details.Where(d => d.AccountType == "Expense").Sum(d => d.Balance);

        return new FinancialReportDto
        {
            AsOfDate = toDate,
            NetIncome = totalRevenue - totalExpense,
            Details = details
        };
    }
}
