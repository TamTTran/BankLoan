using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Loan;

namespace LoanFlow.Application.Services;

public interface IVirtualAccountMatchingService
{
    Task<Result> ProcessIncomingPaymentAsync(string virtualAccountNo, decimal amount, string transactionId, DateOnly paymentDate, string remarks);
}
