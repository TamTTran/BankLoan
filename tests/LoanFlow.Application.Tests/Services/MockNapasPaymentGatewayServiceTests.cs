using FluentAssertions;
using LoanFlow.Application.Common;
using LoanFlow.Application.Services;
using LoanFlow.Application.Tests.Support;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LoanFlow.Application.Tests.Services;

// ─────────────────────────────────────────────────────────────────────────────
// Inline stub — replicates MockNapasPaymentGatewayService so tests don't need
// a project reference to LoanFlow.API (avoids package version conflicts).
// Keep in sync with src/LoanFlow.API/ExternalServices/MockNapasPaymentGatewayService.cs
// ─────────────────────────────────────────────────────────────────────────────

internal sealed class StubNapasGateway : IPaymentGatewayService
{
    private readonly LoanFlowDbContext _db;
    private readonly ILogger _logger;
    private const string BankCodeFail  = "FAIL";
    private const string BankCodeAsync = "ASYNC";

    public StubNapasGateway(LoanFlowDbContext db, ILogger logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<Result<PaymentGatewayResponse>> TransferToCustomerAsync(
        string bankCode, string accountNumber, decimal amount, string remark)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return Result.Failure<PaymentGatewayResponse>("Bank account number is required.");
        if (string.IsNullOrWhiteSpace(bankCode))
            return Result.Failure<PaymentGatewayResponse>("Bank code is required.");
        if (amount <= 0)
            return Result.Failure<PaymentGatewayResponse>("Transfer amount must be greater than zero.");
        if (!NapasBankCode.IsValid(bankCode) && bankCode != BankCodeFail && bankCode != BankCodeAsync)
            return Result.Failure<PaymentGatewayResponse>($"Bank code '{bankCode}' is not a valid NAPAS member bank code.");

        await Task.Delay(1); // minimal latency in tests
        var referenceId = $"LF-{Guid.NewGuid():N}";

        if (bankCode.Equals(BankCodeFail, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<PaymentGatewayResponse>("MOCK: NAPAS rejected (simulated failure). ErrorCode: NSF_001");

        if (bankCode.Equals(BankCodeAsync, StringComparison.OrdinalIgnoreCase))
            return Result.Success(new PaymentGatewayResponse
            {
                TransactionId = string.Empty, ReferenceId = referenceId,
                ProcessedAt = DateTime.UtcNow, OriginalBankCode = bankCode,
                OriginalAccountNumber = accountNumber, IsConfirmedImmediately = false
            });

        return Result.Success(new PaymentGatewayResponse
        {
            TransactionId = $"NAPAS-MOCK-{Guid.NewGuid():N}", ReferenceId = referenceId,
            ProcessedAt = DateTime.UtcNow, OriginalBankCode = bankCode,
            OriginalAccountNumber = accountNumber, IsConfirmedImmediately = true
        });
    }

    public async Task<Result<CallbackHandleResult>> HandleCallbackAsync(NapasCallbackPayload payload)
    {
        var disbursement = await _db.LoanDisbursements
            .FirstOrDefaultAsync(d => d.NapasReferenceId == payload.ReferenceId);

        if (disbursement is null)
            return Result.Failure<CallbackHandleResult>(
                $"No disbursement found for ReferenceId '{payload.ReferenceId}'.");

        if (disbursement.DisbursementStatus == DisbursementStatus.Completed ||
            disbursement.DisbursementStatus == DisbursementStatus.Failed)
        {
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

        return Result.Success(new CallbackHandleResult
        {
            DisbursementId = disbursement.Id,
            NewStatus      = disbursement.DisbursementStatus,
            IsSuccess      = isSuccess
        });
    }

    public async Task<Result<PaymentGatewayResponse>> QueryTransactionStatusAsync(string napasReferenceId)
    {
        await Task.Delay(1);
        var disbursement = await _db.LoanDisbursements
            .FirstOrDefaultAsync(d => d.NapasReferenceId == napasReferenceId);

        if (disbursement is null)
            return Result.Failure<PaymentGatewayResponse>(
                $"[MOCK-NAPAS] No disbursement found for ReferenceId '{napasReferenceId}'.");

        return Result.Success(new PaymentGatewayResponse
        {
            TransactionId         = disbursement.NapasTransactionId ?? string.Empty,
            ReferenceId           = napasReferenceId,
            ProcessedAt           = disbursement.GatewayConfirmedAt ?? DateTime.UtcNow,
            OriginalBankCode      = string.Empty,
            OriginalAccountNumber = string.Empty,
            IsConfirmedImmediately = disbursement.DisbursementStatus == DisbursementStatus.Completed
        });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Tests
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Unit tests cho MockNapasPaymentGatewayService logic.
/// Sử dụng StubNapasGateway (file-scoped class) — replicates production mock
/// mà không cần project reference đến LoanFlow.API.
/// </summary>
public class MockNapasPaymentGatewayServiceTests
{
    private static LoanFlowDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<LoanFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static StubNapasGateway Svc(LoanFlowDbContext db) =>
        new(db, NullLogger.Instance);

    // ── TransferToCustomerAsync ───────────────────────────────────────────────

    [Fact]
    public async Task TransferToCustomerAsync_ValidBankCode_ShouldReturnSyncConfirmed()
    {
        await using var db = CreateDb();

        var result = await Svc(db).TransferToCustomerAsync(NapasBankCode.VCB, "1234567890", 5_000_000m, "Test");

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsConfirmedImmediately.Should().BeTrue();
        result.Value.ReferenceId.Should().StartWith("LF-");
        result.Value.TransactionId.Should().NotBeNullOrEmpty();
        result.Value.OriginalBankCode.Should().Be(NapasBankCode.VCB);
    }

    [Fact]
    public async Task TransferToCustomerAsync_BankCodeFAIL_ShouldSimulateGatewayError()
    {
        await using var db = CreateDb();

        var result = await Svc(db).TransferToCustomerAsync("FAIL", "1234567890", 5_000_000m, "Fail test");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("NSF_001");
    }

    [Fact]
    public async Task TransferToCustomerAsync_BankCodeASYNC_ShouldReturnPendingResponse()
    {
        await using var db = CreateDb();

        var result = await Svc(db).TransferToCustomerAsync("ASYNC", "1234567890", 5_000_000m, "Async test");

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsConfirmedImmediately.Should().BeFalse();
        result.Value.TransactionId.Should().BeEmpty();
        result.Value.ReferenceId.Should().StartWith("LF-");
    }

    [Fact]
    public async Task TransferToCustomerAsync_EmptyAccountNumber_ShouldReturnValidationError()
    {
        await using var db = CreateDb();
        var result = await Svc(db).TransferToCustomerAsync(NapasBankCode.VCB, "", 1_000_000m, "Test");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("account number");
    }

    [Fact]
    public async Task TransferToCustomerAsync_ZeroAmount_ShouldReturnValidationError()
    {
        await using var db = CreateDb();
        var result = await Svc(db).TransferToCustomerAsync(NapasBankCode.VCB, "9876543210", 0m, "Test");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("greater than zero");
    }

    [Fact]
    public async Task TransferToCustomerAsync_NegativeAmount_ShouldReturnValidationError()
    {
        await using var db = CreateDb();
        var result = await Svc(db).TransferToCustomerAsync(NapasBankCode.VCB, "9876543210", -100m, "Test");
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("greater than zero");
    }

    [Fact]
    public async Task TransferToCustomerAsync_InvalidNapasBankCode_ShouldReturnError()
    {
        await using var db = CreateDb();

        var result = await Svc(db).TransferToCustomerAsync("FAKE_BANK", "123456", 1_000_000m, "Test");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not a valid NAPAS");
    }

    // ── HandleCallbackAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task HandleCallbackAsync_SuccessPayload_ShouldMarkDisbursementCompleted()
    {
        await using var db = CreateDb();
        db.LoanDisbursements.Add(new LoanDisbursement
        {
            LoanId = 1, DisbursedAmount = 5_000_000m, DisbursementDate = new DateOnly(2026, 5, 1),
            RecordedByUserId = 1, DisbursementMethod = "BankTransfer",
            DisbursementStatus = DisbursementStatus.Pending,
            NapasReferenceId = "LF-REF-001", CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await Svc(db).HandleCallbackAsync(new NapasCallbackPayload
        {
            ReferenceId = "LF-REF-001", NapasTransactionId = "NAPAS-TX-001",
            Status = "SUCCESS", ProcessedAt = DateTime.UtcNow
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsSuccess.Should().BeTrue();
        result.Value.NewStatus.Should().Be(DisbursementStatus.Completed);

        var updated = await db.LoanDisbursements.FirstAsync();
        updated.DisbursementStatus.Should().Be(DisbursementStatus.Completed);
        updated.NapasTransactionId.Should().Be("NAPAS-TX-001");
    }

    [Fact]
    public async Task HandleCallbackAsync_FailurePayload_ShouldMarkDisbursementFailed()
    {
        await using var db = CreateDb();
        db.LoanDisbursements.Add(new LoanDisbursement
        {
            LoanId = 1, DisbursedAmount = 5_000_000m, DisbursementDate = new DateOnly(2026, 5, 1),
            RecordedByUserId = 1, DisbursementMethod = "BankTransfer",
            DisbursementStatus = DisbursementStatus.Pending,
            NapasReferenceId = "LF-REF-FAIL", CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await Svc(db).HandleCallbackAsync(new NapasCallbackPayload
        {
            ReferenceId = "LF-REF-FAIL", NapasTransactionId = string.Empty,
            Status = "FAILED", ErrorCode = "BANK_REJECT_001",
            ErrorMessage = "Account not found", ProcessedAt = DateTime.UtcNow
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsSuccess.Should().BeFalse();
        result.Value.NewStatus.Should().Be(DisbursementStatus.Failed);

        var updated = await db.LoanDisbursements.FirstAsync();
        updated.GatewayErrorMessage.Should().Contain("BANK_REJECT_001");
    }

    [Fact]
    public async Task HandleCallbackAsync_DuplicateSuccess_ShouldBeIdempotent()
    {
        await using var db = CreateDb();
        db.LoanDisbursements.Add(new LoanDisbursement
        {
            LoanId = 1, DisbursedAmount = 5_000_000m, DisbursementDate = new DateOnly(2026, 5, 1),
            RecordedByUserId = 1, DisbursementMethod = "BankTransfer",
            DisbursementStatus = DisbursementStatus.Completed, // already done
            NapasReferenceId = "LF-DUP-001", NapasTransactionId = "NAPAS-ORIGINAL",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await Svc(db).HandleCallbackAsync(new NapasCallbackPayload
        {
            ReferenceId = "LF-DUP-001", NapasTransactionId = "NAPAS-DUPLICATE",
            Status = "SUCCESS", ProcessedAt = DateTime.UtcNow
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.NewStatus.Should().Be(DisbursementStatus.Completed);

        // Verify original TX ID was NOT overwritten (idempotency)
        var unchanged = await db.LoanDisbursements.FirstAsync();
        unchanged.NapasTransactionId.Should().Be("NAPAS-ORIGINAL");
    }

    [Fact]
    public async Task HandleCallbackAsync_UnknownReference_ShouldReturnFailure()
    {
        await using var db = CreateDb();

        var result = await Svc(db).HandleCallbackAsync(new NapasCallbackPayload
        {
            ReferenceId = "NON-EXISTENT", NapasTransactionId = "NAPAS-X",
            Status = "SUCCESS", ProcessedAt = DateTime.UtcNow
        });

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("NON-EXISTENT");
    }

    // ── QueryTransactionStatusAsync ───────────────────────────────────────────

    [Fact]
    public async Task QueryTransactionStatusAsync_CompletedDisbursement_ShouldReturnConfirmed()
    {
        await using var db = CreateDb();
        db.LoanDisbursements.Add(new LoanDisbursement
        {
            LoanId = 1, DisbursedAmount = 10_000_000m, DisbursementDate = new DateOnly(2026, 5, 1),
            RecordedByUserId = 1, DisbursementMethod = "BankTransfer",
            DisbursementStatus = DisbursementStatus.Completed,
            NapasReferenceId = "LF-QUERY-001", NapasTransactionId = "NAPAS-QUERY-TX",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await Svc(db).QueryTransactionStatusAsync("LF-QUERY-001");

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsConfirmedImmediately.Should().BeTrue();
        result.Value.TransactionId.Should().Be("NAPAS-QUERY-TX");
    }

    [Fact]
    public async Task QueryTransactionStatusAsync_PendingDisbursement_ShouldReturnNotConfirmed()
    {
        await using var db = CreateDb();
        db.LoanDisbursements.Add(new LoanDisbursement
        {
            LoanId = 1, DisbursedAmount = 10_000_000m, DisbursementDate = new DateOnly(2026, 5, 1),
            RecordedByUserId = 1, DisbursementMethod = "BankTransfer",
            DisbursementStatus = DisbursementStatus.Pending,
            NapasReferenceId = "LF-PENDING-001",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var result = await Svc(db).QueryTransactionStatusAsync("LF-PENDING-001");

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsConfirmedImmediately.Should().BeFalse();
    }

    [Fact]
    public async Task QueryTransactionStatusAsync_NonExistent_ShouldReturnFailure()
    {
        await using var db = CreateDb();

        var result = await Svc(db).QueryTransactionStatusAsync("NOT-FOUND-9999");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("NOT-FOUND-9999");
    }
}
