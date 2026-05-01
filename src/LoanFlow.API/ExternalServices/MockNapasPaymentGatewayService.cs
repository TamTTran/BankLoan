using LoanFlow.Application.Common;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoanFlow.API.ExternalServices;

/// <summary>
/// Mock NAPAS Payment Gateway — dùng trong môi trường Development / khi chưa có sandbox credentials.
///
/// Hành vi giả lập:
///   - TransferToCustomerAsync: Luôn SUCCESS sau 500ms latency (sync path).
///   - QueryTransactionStatusAsync: Tra theo ReferenceId trong DB, trả trạng thái thực tế.
///   - HandleCallbackAsync: Cập nhật DisbursementStatus trong DB.
///
/// Để giả lập FAILURE: set bankCode = "FAIL" trong Customer.BankCode.
/// Để giả lập PENDING (async path): set bankCode = "ASYNC" — trả về Pending, cần gọi callback thủ công.
/// </summary>
public class MockNapasPaymentGatewayService : IPaymentGatewayService
{
    private readonly LoanFlowDbContext _db;
    private readonly ILogger<MockNapasPaymentGatewayService> _logger;

    // Special bank codes để giả lập các scenario
    private const string BankCodeSimulateFailure = "FAIL";
    private const string BankCodeSimulateAsync   = "ASYNC";

    public MockNapasPaymentGatewayService(
        LoanFlowDbContext db,
        ILogger<MockNapasPaymentGatewayService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── [SYNC] TransferToCustomerAsync ──────────────────────────────────────

    public async Task<Result<PaymentGatewayResponse>> TransferToCustomerAsync(
        string bankCode, string accountNumber, decimal amount, string remark)
    {
        // Basic validation (same as real impl)
        if (string.IsNullOrWhiteSpace(accountNumber))
            return Result.Failure<PaymentGatewayResponse>("Bank account number is required.");
        if (string.IsNullOrWhiteSpace(bankCode))
            return Result.Failure<PaymentGatewayResponse>("Bank code is required.");
        if (amount <= 0)
            return Result.Failure<PaymentGatewayResponse>("Transfer amount must be greater than zero.");
        if (!NapasBankCode.IsValid(bankCode) && bankCode != BankCodeSimulateFailure && bankCode != BankCodeSimulateAsync)
            return Result.Failure<PaymentGatewayResponse>($"Bank code '{bankCode}' is not a valid NAPAS member bank code.");

        // Simulate network latency
        await Task.Delay(500);

        var referenceId = GenerateReferenceId();

        // Scenario: FAIL — simulate gateway error
        if (bankCode.Equals(BankCodeSimulateFailure, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[MOCK-NAPAS] Simulating FAILURE for BankCode=FAIL. ReferenceId={ReferenceId}", referenceId);
            return Result.Failure<PaymentGatewayResponse>("MOCK: NAPAS rejected the transfer (simulated failure). ErrorCode: NSF_001");
        }

        // Scenario: ASYNC — simulate pending async path (callback required)
        if (bankCode.Equals(BankCodeSimulateAsync, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[MOCK-NAPAS] Simulating ASYNC path. ReferenceId={ReferenceId} — status=Pending, call /api/napas/callback to confirm.", referenceId);
            return Result.Success(new PaymentGatewayResponse
            {
                TransactionId     = string.Empty,     // Chưa có — NAPAS chưa xác nhận
                ReferenceId       = referenceId,
                ProcessedAt       = DateTime.UtcNow,
                OriginalBankCode  = bankCode,
                OriginalAccountNumber = accountNumber,
                IsConfirmedImmediately = false        // → Caller phải set DisbursementStatus=Pending
            });
        }

        // Default scenario: SUCCESS (sync confirmed)
        var napasTransactionId = $"NAPAS-MOCK-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(10000, 99999)}";
        _logger.LogInformation(
            "[MOCK-NAPAS] Transfer SUCCESS. BankCode={BankCode} Account={Account} Amount={Amount:N0} ReferenceId={ReferenceId} NapasTxId={TxId}",
            bankCode, accountNumber, amount, referenceId, napasTransactionId);

        return Result.Success(new PaymentGatewayResponse
        {
            TransactionId          = napasTransactionId,
            ReferenceId            = referenceId,
            ProcessedAt            = DateTime.UtcNow,
            OriginalBankCode       = bankCode,
            OriginalAccountNumber  = accountNumber,
            IsConfirmedImmediately = true
        });
    }

    // ── [ASYNC CALLBACK] HandleCallbackAsync ────────────────────────────────

    public async Task<Result<CallbackHandleResult>> HandleCallbackAsync(NapasCallbackPayload payload)
    {
        _logger.LogInformation(
            "[MOCK-NAPAS] HandleCallback: ReferenceId={ReferenceId} Status={Status} NapasTxId={NapasTxId}",
            payload.ReferenceId, payload.Status, payload.NapasTransactionId);

        // Find disbursement by NapasReferenceId
        var disbursement = await _db.LoanDisbursements
            .FirstOrDefaultAsync(d => d.NapasReferenceId == payload.ReferenceId);

        if (disbursement is null)
            return Result.Failure<CallbackHandleResult>(
                $"No disbursement found for ReferenceId '{payload.ReferenceId}'.");

        if (disbursement.DisbursementStatus == DisbursementStatus.Completed ||
            disbursement.DisbursementStatus == DisbursementStatus.Failed)
        {
            _logger.LogWarning("[MOCK-NAPAS] Duplicate callback for ReferenceId={ReferenceId}. Already {Status}.",
                payload.ReferenceId, disbursement.DisbursementStatus);
            return Result.Success(new CallbackHandleResult
            {
                DisbursementId = disbursement.Id,
                NewStatus      = disbursement.DisbursementStatus,
                IsSuccess      = disbursement.DisbursementStatus == DisbursementStatus.Completed
            });
        }

        var isSuccess = payload.Status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase);
        disbursement.DisbursementStatus  = isSuccess ? DisbursementStatus.Completed : DisbursementStatus.Failed;
        disbursement.NapasTransactionId  = payload.NapasTransactionId;
        disbursement.GatewayConfirmedAt  = payload.ProcessedAt;
        disbursement.GatewayErrorMessage = isSuccess ? null : $"{payload.ErrorCode}: {payload.ErrorMessage}";

        await _db.SaveChangesAsync();

        _logger.LogInformation("[MOCK-NAPAS] Disbursement {Id} status → {Status}.", disbursement.Id, disbursement.DisbursementStatus);

        return Result.Success(new CallbackHandleResult
        {
            DisbursementId = disbursement.Id,
            NewStatus      = disbursement.DisbursementStatus,
            IsSuccess      = isSuccess
        });
    }

    // ── [QUERY] QueryTransactionStatusAsync ─────────────────────────────────

    public async Task<Result<PaymentGatewayResponse>> QueryTransactionStatusAsync(string napasReferenceId)
    {
        await Task.Delay(200); // simulate network

        var disbursement = await _db.LoanDisbursements
            .FirstOrDefaultAsync(d => d.NapasReferenceId == napasReferenceId);

        if (disbursement is null)
            return Result.Failure<PaymentGatewayResponse>($"[MOCK-NAPAS] No disbursement found for ReferenceId '{napasReferenceId}'.");

        _logger.LogInformation("[MOCK-NAPAS] QueryStatus ReferenceId={ReferenceId} → CurrentStatus={Status}",
            napasReferenceId, disbursement.DisbursementStatus);

        return Result.Success(new PaymentGatewayResponse
        {
            TransactionId          = disbursement.NapasTransactionId ?? string.Empty,
            ReferenceId            = napasReferenceId,
            ProcessedAt            = disbursement.GatewayConfirmedAt ?? DateTime.UtcNow,
            OriginalBankCode       = string.Empty,
            OriginalAccountNumber  = string.Empty,
            IsConfirmedImmediately = disbursement.DisbursementStatus == DisbursementStatus.Completed
        });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string GenerateReferenceId() =>
        $"LF-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
}
