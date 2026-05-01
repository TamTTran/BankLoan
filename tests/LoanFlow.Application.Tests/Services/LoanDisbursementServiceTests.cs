using FluentAssertions;
using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Loan;
using LoanFlow.Application.Services;
using LoanFlow.Application.Tests.Support;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LoanFlow.Application.Tests.Services;

/// <summary>
/// Unit tests cho LoanDisbursementService.
/// Dùng InMemoryDatabase + mock IPaymentGatewayService, IAccountingService.
/// Covers: CreateLoanAsync, DisburseAsync (3 scenarios NAPAS).
/// </summary>
public class LoanDisbursementServiceTests
{
    // ── Shared test fixtures ─────────────────────────────────────────────────

    private static readonly TestBusinessClock _clock = new(
        utcNow:       new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
        localNow:     new DateTime(2026, 5, 1, 7, 0, 0, DateTimeKind.Unspecified),
        businessDate: new DateOnly(2026, 5, 1));

    private static LoanFlowDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<LoanFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static Mock<IAccountingService> MockAccounting()
    {
        var m = new Mock<IAccountingService>();
        m.Setup(x => x.GetAccountCodeAsync(It.IsAny<string>(), It.IsAny<bool>()))
         .ReturnsAsync("ACC-001");
        m.Setup(x => x.PostJournalAsync(It.IsAny<JournalEntryDto>()))
         .ReturnsAsync(Result.Success(new JournalEntry()));
        return m;
    }

    /// <summary>Seed một Application đã được Approved để tạo Loan từ đó.</summary>
    private static async Task<(LoanProduct product, LoanApplication application)>
        SeedApprovedApplicationAsync(LoanFlowDbContext db, decimal amount = 10_000_000m, int tenor = 12)
    {
        var product = new LoanProduct
        {
            ProductName          = "Personal Loan",
            ProductType          = "PersonalUnsecured",
            MonthlyInterestRate  = 0.01m,
            ProcessingFee        = 0m,
            InterestMethod       = InterestMethod.ReducingBalance,
            MinAmount            = 1_000_000m,
            MaxAmount            = 100_000_000m,
            AllowedTenorsJson    = "[6,12,18,24,36]",
            RequiresCollateral   = false,
            EarlySettlementFeeRate = 0.01m,
            LateFeeRate          = 0.001m,
            LateFeeGraceDays     = 3
        };
        db.LoanProducts.Add(product);

        var customer = new Customer
        {
            FullName         = "Nguyễn Test",
            Phone            = "0901234567",
            IdNumber         = "079001234567",
            Address          = "Hà Nội",
            DateOfBirth      = new DateTime(1990, 1, 1),
            Nationality      = "VN",
            CreatedByUserId  = 1,
            AssignedToUserId = 1
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var application = new LoanApplication
        {
            CustomerId           = customer.Id,
            ProductId            = product.Id,
            RequestedAmount      = amount,
            RequestedTenorMonths = tenor,
            Status               = ApplicationStatus.Approved,
            Purpose              = "Tiêu dùng",
            CreatedByUserId      = 1
        };
        db.LoanApplications.Add(application);
        await db.SaveChangesAsync();

        return (product, application);
    }

    // ── CreateLoanAsync Tests ────────────────────────────────────────────────

    [Fact]
    public async Task CreateLoanAsync_WithApprovedApplication_ShouldCreateLoan()
    {
        await using var db = CreateDb();
        var (_, app) = await SeedApprovedApplicationAsync(db);

        var queryService = new LoanQueryService(db, _clock);
        var gatewayMock  = new Mock<IPaymentGatewayService>();
        var svc = new LoanDisbursementService(db, _clock, MockAccounting().Object, gatewayMock.Object, queryService);

        var result = await svc.CreateLoanAsync(app.Id, userId: 1);

        result.IsSuccess.Should().BeTrue();
        result.Value!.LoanAccountNo.Should().StartWith($"LN-2026-");
        result.Value.Status.Should().Be(LoanStatus.PendingDisbursement);
        result.Value.ApprovedAmount.Should().Be(10_000_000m);

        var loan = await db.Loans.FirstAsync();
        loan.VirtualAccountNo.Should().StartWith("VA");
    }

    [Fact]
    public async Task CreateLoanAsync_WithPendingApplication_ShouldReturnFailure()
    {
        await using var db = CreateDb();
        var (_, app) = await SeedApprovedApplicationAsync(db);
        app.Status = ApplicationStatus.Submitted;
        await db.SaveChangesAsync();

        var queryService = new LoanQueryService(db, _clock);
        var svc = new LoanDisbursementService(db, _clock, MockAccounting().Object,
            new Mock<IPaymentGatewayService>().Object, queryService);

        var result = await svc.CreateLoanAsync(app.Id, userId: 1);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("LOAN-VAL-001");
    }

    [Fact]
    public async Task CreateLoanAsync_WhenLoanAlreadyExists_ShouldReturnFailure()
    {
        await using var db = CreateDb();
        var (_, app) = await SeedApprovedApplicationAsync(db);

        var queryService = new LoanQueryService(db, _clock);
        var svc = new LoanDisbursementService(db, _clock, MockAccounting().Object,
            new Mock<IPaymentGatewayService>().Object, queryService);

        // Create loan once
        await svc.CreateLoanAsync(app.Id, userId: 1);
        // Try to create again
        var result = await svc.CreateLoanAsync(app.Id, userId: 1);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("LOAN-VAL-002");
    }

    // ── DisburseAsync Tests ───────────────────────────────────────────────────

    /// <summary>Seed a Loan in PendingDisbursement with E-Contract signed.</summary>
    private static async Task<Loan> SeedLoanReadyForDisbursementAsync(
        LoanFlowDbContext db,
        string? bankCode       = null,
        string? bankAccountNo  = null,
        decimal amount         = 10_000_000m,
        int tenor              = 12)
    {
        var (product, app) = await SeedApprovedApplicationAsync(db, amount, tenor);
        var customer = await db.Customers.FirstAsync();
        customer.BankCode          = bankCode;
        customer.BankAccountNumber = bankAccountNo;

        var loan = new Loan
        {
            LoanAccountNo       = "LN-2026-0001",
            ApplicationId       = app.Id,
            CustomerId          = customer.Id,
            ProductId           = product.Id,
            ApprovedAmount      = amount,
            TenorMonths         = tenor,
            MonthlyInterestRate = product.MonthlyInterestRate,
            ProcessingFee       = product.ProcessingFee,
            Status              = LoanStatus.PendingDisbursement,
            OutstandingAmount   = amount,
            InterestMethod      = InterestMethod.ReducingBalance,
            EarlySettlementFeeRate = product.EarlySettlementFeeRate,
            LateFeeRate         = product.LateFeeRate,
            LateFeeGraceDays    = product.LateFeeGraceDays,
            ProductType         = product.ProductType,
            VirtualAccountNo    = "VA00000001"
        };
        db.Loans.Add(loan);

        // Sign E-Contract (all required fields)
        db.EContracts.Add(new EContract
        {
            LoanId           = 0, // will be fixed after save
            ContractNumber   = $"CTR-{Guid.NewGuid():N}",
            PdfContentHash   = "sha256-mock-hash",
            DigitalSignature = "mock-signature",
            ClientIpAddress  = "127.0.0.1"
        });

        await db.SaveChangesAsync();

        // Fix LoanId FK after save
        var contract = await db.EContracts.FirstAsync();
        contract.LoanId = loan.Id;
        await db.SaveChangesAsync();

        return loan;
    }

    [Fact]
    public async Task DisburseAsync_CashDisbursement_ShouldActivateLoanAndGenerateSchedule()
    {
        await using var db = CreateDb();
        var loan = await SeedLoanReadyForDisbursementAsync(db);

        var queryService = new LoanQueryService(db, _clock);
        var gatewayMock  = new Mock<IPaymentGatewayService>();
        var svc = new LoanDisbursementService(db, _clock, MockAccounting().Object, gatewayMock.Object, queryService);

        var disbursementDate = new DateOnly(2026, 5, 1);
        var result = await svc.DisburseAsync(loan.Id, new DisburseRequest
        {
            DisbursedAmount  = 10_000_000m,
            DisbursementDate = disbursementDate
        }, userId: 1);

        result.IsSuccess.Should().BeTrue();

        var updatedLoan = await db.Loans.FindAsync(loan.Id);
        updatedLoan!.Status.Should().Be(LoanStatus.Active);
        updatedLoan.DisbursementDate.Should().Be(disbursementDate);
        updatedLoan.MaturityDate.Should().Be(disbursementDate.AddMonths(12));

        var scheduleCount = await db.RepaymentSchedules.CountAsync(s => s.LoanId == loan.Id);
        scheduleCount.Should().Be(12);

        // NAPAS gateway should NOT be called (no bank account on customer)
        gatewayMock.Verify(x => x.TransferToCustomerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task DisburseAsync_WithBankAccount_ShouldCallNapasAndSetCompleted()
    {
        await using var db = CreateDb();
        var loan = await SeedLoanReadyForDisbursementAsync(db, bankCode: "VCB", bankAccountNo: "1234567890");

        var queryService = new LoanQueryService(db, _clock);
        var gatewayMock  = new Mock<IPaymentGatewayService>();
        gatewayMock.Setup(x => x.TransferToCustomerAsync(
                "VCB", "1234567890", 10_000_000m, It.IsAny<string>()))
            .ReturnsAsync(Result.Success(new PaymentGatewayResponse
            {
                TransactionId          = "NAPAS-TX-001",
                ReferenceId            = "LF-REF-001",
                ProcessedAt            = DateTime.UtcNow,
                OriginalBankCode       = "VCB",
                OriginalAccountNumber  = "1234567890",
                IsConfirmedImmediately = true
            }));

        var svc = new LoanDisbursementService(db, _clock, MockAccounting().Object, gatewayMock.Object, queryService);

        var result = await svc.DisburseAsync(loan.Id, new DisburseRequest
        {
            DisbursedAmount  = 10_000_000m,
            DisbursementDate = new DateOnly(2026, 5, 1)
        }, userId: 1);

        result.IsSuccess.Should().BeTrue();

        var disbursement = await db.LoanDisbursements.FirstAsync(d => d.LoanId == loan.Id);
        disbursement.DisbursementStatus.Should().Be(DisbursementStatus.Completed);
        disbursement.NapasTransactionId.Should().Be("NAPAS-TX-001");
        disbursement.NapasReferenceId.Should().Be("LF-REF-001");
    }

    [Fact]
    public async Task DisburseAsync_WithAsyncNapas_ShouldSetPendingStatus()
    {
        await using var db = CreateDb();
        var loan = await SeedLoanReadyForDisbursementAsync(db, bankCode: "MBB", bankAccountNo: "9876543210");

        var queryService = new LoanQueryService(db, _clock);
        var gatewayMock  = new Mock<IPaymentGatewayService>();
        // Simulate ASYNC path: gateway returns pending (no TransactionId yet)
        gatewayMock.Setup(x => x.TransferToCustomerAsync(
                "MBB", "9876543210", 10_000_000m, It.IsAny<string>()))
            .ReturnsAsync(Result.Success(new PaymentGatewayResponse
            {
                TransactionId          = string.Empty,
                ReferenceId            = "LF-ASYNC-001",
                ProcessedAt            = DateTime.UtcNow,
                OriginalBankCode       = "MBB",
                OriginalAccountNumber  = "9876543210",
                IsConfirmedImmediately = false
            }));

        var svc = new LoanDisbursementService(db, _clock, MockAccounting().Object, gatewayMock.Object, queryService);

        var result = await svc.DisburseAsync(loan.Id, new DisburseRequest
        {
            DisbursedAmount  = 10_000_000m,
            DisbursementDate = new DateOnly(2026, 5, 1)
        }, userId: 1);

        result.IsSuccess.Should().BeTrue();

        var disbursement = await db.LoanDisbursements.FirstAsync(d => d.LoanId == loan.Id);
        // Status remains Pending until callback arrives
        disbursement.DisbursementStatus.Should().Be(DisbursementStatus.Pending);
        disbursement.NapasReferenceId.Should().Be("LF-ASYNC-001");
        disbursement.NapasTransactionId.Should().BeNull();
    }

    [Fact]
    public async Task DisburseAsync_WhenNapasFails_ShouldMarkFailedAndCreateReversal()
    {
        await using var db = CreateDb();
        var loan = await SeedLoanReadyForDisbursementAsync(db, bankCode: "ACB", bankAccountNo: "111111111");

        var queryService = new LoanQueryService(db, _clock);
        var gatewayMock  = new Mock<IPaymentGatewayService>();
        gatewayMock.Setup(x => x.TransferToCustomerAsync(
                "ACB", "111111111", 10_000_000m, It.IsAny<string>()))
            .ReturnsAsync(Result.Failure<PaymentGatewayResponse>("MOCK: NSF_001 — insufficient funds"));

        var svc = new LoanDisbursementService(db, _clock, MockAccounting().Object, gatewayMock.Object, queryService);

        var result = await svc.DisburseAsync(loan.Id, new DisburseRequest
        {
            DisbursedAmount  = 10_000_000m,
            DisbursementDate = new DateOnly(2026, 5, 1)
        }, userId: 1);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("NAPAS Auto-Disbursement Failed");

        var disbursement = await db.LoanDisbursements.FirstAsync(d => d.LoanId == loan.Id);
        disbursement.DisbursementStatus.Should().Be(DisbursementStatus.Failed);

        var reversalCount = await db.TransactionReversals.CountAsync(r => r.LoanId == loan.Id);
        reversalCount.Should().Be(1);
    }

    [Fact]
    public async Task DisburseAsync_WhenEodRunning_ShouldReturnFailure()
    {
        await using var db = CreateDb();
        db.SystemConfigs.Add(new SystemConfig { Key = "IsEodRunning", Value = "true" });
        var loan = await SeedLoanReadyForDisbursementAsync(db);
        await db.SaveChangesAsync();

        var queryService = new LoanQueryService(db, _clock);
        var svc = new LoanDisbursementService(db, _clock, MockAccounting().Object,
            new Mock<IPaymentGatewayService>().Object, queryService);

        var result = await svc.DisburseAsync(loan.Id, new DisburseRequest
        {
            DisbursedAmount  = 10_000_000m,
            DisbursementDate = new DateOnly(2026, 5, 1)
        }, userId: 1);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("EOD");
    }

    [Fact]
    public async Task DisburseAsync_WhenAmountMismatch_ShouldReturnValidationError()
    {
        await using var db = CreateDb();
        var loan = await SeedLoanReadyForDisbursementAsync(db);

        var queryService = new LoanQueryService(db, _clock);
        var svc = new LoanDisbursementService(db, _clock, MockAccounting().Object,
            new Mock<IPaymentGatewayService>().Object, queryService);

        var result = await svc.DisburseAsync(loan.Id, new DisburseRequest
        {
            DisbursedAmount  = 5_000_000m, // < approved amount
            DisbursementDate = new DateOnly(2026, 5, 1)
        }, userId: 1);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("DISB-VAL-002");
    }

    [Fact]
    public async Task DisburseAsync_WhenNoEContract_ShouldReturnValidationError()
    {
        await using var db = CreateDb();
        var loan = await SeedLoanReadyForDisbursementAsync(db);

        // Remove the e-contract
        var contracts = db.EContracts.ToList();
        db.EContracts.RemoveRange(contracts);
        await db.SaveChangesAsync();

        var queryService = new LoanQueryService(db, _clock);
        var svc = new LoanDisbursementService(db, _clock, MockAccounting().Object,
            new Mock<IPaymentGatewayService>().Object, queryService);

        var result = await svc.DisburseAsync(loan.Id, new DisburseRequest
        {
            DisbursedAmount  = 10_000_000m,
            DisbursementDate = new DateOnly(2026, 5, 1)
        }, userId: 1);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("APP-VAL-006");
    }
}
