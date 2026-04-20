using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface IPaymentGatewayService
{
    Task<Result<PaymentGatewayResponse>> TransferToCustomerAsync(string bankCode, string accountNumber, decimal amount, string remark);
}

public class PaymentGatewayResponse
{
    public string TransactionId { get; set; } = default!;
    public DateTime ProcessedAt { get; set; }
    public string OriginalBankCode { get; set; } = default!;
    public string OriginalAccountNumber { get; set; } = default!;
}

// Mock Implementation for Phase 4 (P1)
public class MockPaymentGatewayService : IPaymentGatewayService
{
    public async Task<Result<PaymentGatewayResponse>> TransferToCustomerAsync(string bankCode, string accountNumber, decimal amount, string remark)
    {
        // 1. Simulate NAPAS / Bank Gateway Latency
        await Task.Delay(1500); 

        // 2. Validate basics
        if (string.IsNullOrWhiteSpace(accountNumber))
            return Result.Failure<PaymentGatewayResponse>("Bank account number is required for auto-disbursement.");
            
        if (string.IsNullOrWhiteSpace(bankCode))
            return Result.Failure<PaymentGatewayResponse>("Bank code (e.g., VCB, TCB) is required.");

        if (amount <= 0)
            return Result.Failure<PaymentGatewayResponse>("Transfer amount must be greater than zero.");

        // 3. Mock Success Response
        return Result.Success(new PaymentGatewayResponse
        {
            TransactionId = $"NAPAS-{DateTime.UtcNow:yyyyMMddHHmmss}-{new Random().Next(1000, 9999)}",
            ProcessedAt = DateTime.UtcNow,
            OriginalBankCode = bankCode,
            OriginalAccountNumber = accountNumber
        });
    }
}
