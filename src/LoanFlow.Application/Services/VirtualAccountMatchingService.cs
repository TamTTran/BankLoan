using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Loan;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoanFlow.Application.Services;

public interface IVirtualAccountMatchingService
{
    Task<Result> ProcessIncomingPaymentAsync(string virtualAccountNo, decimal amount, string transactionId, DateOnly paymentDate, string remarks);
}

public class VirtualAccountMatchingService : IVirtualAccountMatchingService
{
    private readonly LoanFlowDbContext _db;
    private readonly ILoanPaymentService _loanPaymentService;
    private readonly ILogger<VirtualAccountMatchingService> _logger;

    public VirtualAccountMatchingService(LoanFlowDbContext db, ILoanPaymentService loanPaymentService, ILogger<VirtualAccountMatchingService> logger)
    {
        _db = db;
        _loanPaymentService = loanPaymentService;
        _logger = logger;
    }

    public async Task<Result> ProcessIncomingPaymentAsync(string virtualAccountNo, decimal amount, string transactionId, DateOnly paymentDate, string remarks)
    {
        // 1. Prevent duplicate processing
        if (await _db.Payments.AnyAsync(p => p.ExternalReferenceId == transactionId))
            return Result.Failure("Payment already processed.");

        // 2. Find Loan by Virtual Account No
        var loan = await _db.Loans
            .FirstOrDefaultAsync(l => l.VirtualAccountNo == virtualAccountNo);

        if (loan == null)
        {
            _logger.LogWarning($"Virtual Account {virtualAccountNo} not found for TxId {transactionId}");
            
            // Move to Suspense Account
            _db.SuspenseTransactions.Add(new SuspenseTransaction
            {
                Amount = amount,
                Reason = $"Unmatched VA: {virtualAccountNo} - {remarks}. Ref: {transactionId}",
                Status = "Unresolved"
            });
            await _db.SaveChangesAsync();
            return Result.Failure($"Virtual account not found. Moved to suspense.");
        }

        // 3. Create Payment and allocate
        var request = new RecordPaymentRequest
        {
            Amount = amount,
            PaymentDate = paymentDate,
            PaymentMethod = "BankTransfer",
            Remarks = $"Auto-matched via VA {virtualAccountNo}. Ref: {transactionId}",
            ExternalReferenceId = transactionId
        };

        var allocationResult = await _loanPaymentService.RecordPaymentAsync(loan.Id, request, 1); // 1 = System user

        return allocationResult;
    }
}
