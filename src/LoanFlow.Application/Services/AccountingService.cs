using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class JournalEntryDto
{
    public string ReferenceNumber { get; set; } = default!;
    public DateOnly BusinessDate { get; set; }
    public string Description { get; set; } = default!;
    public List<JournalEntryLineDto> Lines { get; set; } = new();
}

public class JournalEntryLineDto
{
    public string AccountCode { get; set; } = default!;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? LineDescription { get; set; }
}

public interface IAccountingService
{
    Task<Result<JournalEntry>> PostJournalAsync(JournalEntryDto request);
}

public class AccountingService : IAccountingService
{
    private readonly LoanFlowDbContext _db;

    public AccountingService(LoanFlowDbContext db)
    {
        _db = db;
    }

    public async Task<Result<JournalEntry>> PostJournalAsync(JournalEntryDto request)
    {
        if (request.Lines.Count < 2)
            return Result.Failure<JournalEntry>("A journal entry must have at least two lines.");

        // Enforce Double Entry constraint
        var totalDebits = request.Lines.Sum(l => l.DebitAmount);
        var totalCredits = request.Lines.Sum(l => l.CreditAmount);

        if (totalDebits != totalCredits)
            return Result.Failure<JournalEntry>($"Unbalanced journal entry. Debits ({totalDebits}) != Credits ({totalCredits})");

        var accountCodes = request.Lines.Select(l => l.AccountCode).Distinct().ToList();
        var accounts = await _db.GLAccounts
            .Where(a => accountCodes.Contains(a.AccountCode))
            .ToDictionaryAsync(a => a.AccountCode);

        // Validate all accounts exist
        foreach (var code in accountCodes)
        {
            if (!accounts.ContainsKey(code))
                return Result.Failure<JournalEntry>($"GL Account code '{code}' not found.");
        }

        var entry = new JournalEntry
        {
            ReferenceNumber = request.ReferenceNumber,
            BusinessDate = request.BusinessDate,
            Description = request.Description,
            PostedAt = DateTime.UtcNow
        };

        foreach (var lineDto in request.Lines)
        {
            var account = accounts[lineDto.AccountCode];
            
            entry.Lines.Add(new JournalEntryLine
            {
                GLAccountId = account.Id,
                DebitAmount = lineDto.DebitAmount,
                CreditAmount = lineDto.CreditAmount,
                LineDescription = lineDto.LineDescription
            });

            // Update GL Account Balance based on AccountType
            if (account.AccountType == GLAccountType.Asset || account.AccountType == GLAccountType.Expense)
            {
                // Assets & Expenses increase with debit, decrease with credit
                account.Balance += lineDto.DebitAmount;
                account.Balance -= lineDto.CreditAmount;
            }
            else
            {
                // Liabilities, Equity, Income increase with credit, decrease with debit
                account.Balance += lineDto.CreditAmount;
                account.Balance -= lineDto.DebitAmount;
            }
            
            account.UpdatedAt = DateTime.UtcNow;
        }

        _db.JournalEntries.Add(entry);
        
        // We do NOT call SaveChangesAsync here. 
        // This is meant to be part of an existing transaction (e.g., Disbursement or Payment).
        // The caller will flush the changes.
        
        return Result.Success(entry);
    }
}
