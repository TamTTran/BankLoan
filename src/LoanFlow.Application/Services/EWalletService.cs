using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.EWallet;
using LoanFlow.Application.DTOs.Loan;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoanFlow.Application.Services;

public class EWalletService : IEWalletService
{
    private readonly LoanFlowDbContext _db;
    private readonly IEWalletProvider _provider;
    private readonly ILoanPaymentService _paymentService;
    private readonly IBusinessClock _businessClock;
    private readonly ILogger<EWalletService> _logger;

    public EWalletService(
        LoanFlowDbContext db,
        IEWalletProvider provider,
        ILoanPaymentService paymentService,
        IBusinessClock businessClock,
        ILogger<EWalletService> logger)
    {
        _db = db;
        _provider = provider;
        _paymentService = paymentService;
        _businessClock = businessClock;
        _logger = logger;
    }

    // ── Link Wallet ─────────────────────────────────────────────────────────

    public async Task<Result<CustomerEWalletDto>> LinkWalletAsync(int customerId, LinkEWalletRequest req, int userId)
    {
        // Validate provider
        if (!EWalletProvider.IsValid(req.WalletProvider))
            return Result.Failure<CustomerEWalletDto>($"Unsupported wallet provider '{req.WalletProvider}'. Supported: {string.Join(", ", EWalletProvider.All)}");

        if (string.IsNullOrWhiteSpace(req.WalletAccountId))
            return Result.Failure<CustomerEWalletDto>("Wallet account ID is required.");

        var customer = await _db.Customers.FindAsync(customerId);
        if (customer is null)
            return Result.Failure<CustomerEWalletDto>("Customer not found.");

        // Idempotent: Check nếu đã có ví Active hoặc PendingVerification cùng provider
        var existing = await _db.CustomerEWallets.FirstOrDefaultAsync(w =>
            w.CustomerId == customerId &&
            w.WalletProvider == req.WalletProvider &&
            w.WalletAccountId == req.WalletAccountId &&
            (w.Status == EWalletLinkStatus.Active || w.Status == EWalletLinkStatus.PendingVerification));

        if (existing != null)
        {
            if (existing.Status == EWalletLinkStatus.Active)
                return Result.Failure<CustomerEWalletDto>($"Ví {req.WalletProvider} đã được liên kết với tài khoản này.");

            // PendingVerification → trả lại record để user verify OTP
            return Result.Success(MapToDto(existing));
        }

        // Call provider to initiate linking (ví sẽ gửi OTP cho user)
        EWalletLinkResult linkResult;
        try
        {
            linkResult = await _provider.InitiateLinkingAsync(req.WalletAccountId, customer.FullName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "E-wallet linking failed for customer {CustomerId}, provider {Provider}", customerId, req.WalletProvider);
            return Result.Failure<CustomerEWalletDto>($"Không thể kết nối {req.WalletProvider}: {ex.Message}");
        }

        if (!linkResult.IsSuccess)
            return Result.Failure<CustomerEWalletDto>($"Liên kết thất bại: {linkResult.ErrorMessage}");

        var ewallet = new CustomerEWallet
        {
            CustomerId = customerId,
            WalletProvider = req.WalletProvider,
            WalletAccountId = req.WalletAccountId,
            WalletUserName = linkResult.WalletUserName,
            OtpNonce = linkResult.OtpNonce,
            Status = EWalletLinkStatus.PendingVerification,
            LinkedByUserId = userId
        };

        _db.CustomerEWallets.Add(ewallet);

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.EWalletLinked,
            EntityType = "CustomerEWallet",
            EntityId = 0,
            NewValuesJson = AuditHelper.ToJson(new { customerId, provider = req.WalletProvider, walletAccountId = req.WalletAccountId, status = "PendingVerification" })
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation("E-wallet linking initiated. Customer={CustomerId} Provider={Provider} WalletId={WalletAccountId}",
            customerId, req.WalletProvider, req.WalletAccountId);

        return Result.Success(MapToDto(ewallet));
    }

    // ── Verify OTP ──────────────────────────────────────────────────────────

    public async Task<Result<CustomerEWalletDto>> VerifyLinkingOtpAsync(int customerId, VerifyEWalletOtpRequest req, int userId)
    {
        var ewallet = await _db.CustomerEWallets.FirstOrDefaultAsync(w =>
            w.Id == req.LinkingId && w.CustomerId == customerId);

        if (ewallet is null)
            return Result.Failure<CustomerEWalletDto>("Liên kết ví không tồn tại.");

        if (ewallet.Status != EWalletLinkStatus.PendingVerification)
            return Result.Failure<CustomerEWalletDto>($"Ví đang ở trạng thái '{ewallet.Status}', không thể xác thực OTP.");

        if (string.IsNullOrWhiteSpace(ewallet.OtpNonce))
            return Result.Failure<CustomerEWalletDto>("OTP nonce đã hết hạn. Vui lòng liên kết lại.");

        // Verify OTP with provider
        EWalletVerifyResult verifyResult;
        try
        {
            verifyResult = await _provider.VerifyOtpAsync(ewallet.OtpNonce!, req.OtpCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "E-wallet OTP verification failed for ewallet {EWalletId}", ewallet.Id);
            return Result.Failure<CustomerEWalletDto>($"Xác thực OTP thất bại: {ex.Message}");
        }

        if (!verifyResult.IsSuccess)
            return Result.Failure<CustomerEWalletDto>($"OTP không hợp lệ: {verifyResult.ErrorMessage}");

        ewallet.Status = EWalletLinkStatus.Active;
        ewallet.WalletToken = verifyResult.WalletToken;
        ewallet.WalletUserName = verifyResult.WalletUserName ?? ewallet.WalletUserName;
        ewallet.VerifiedAt = DateTime.UtcNow;
        ewallet.OtpNonce = null; // Clear OTP nonce after verification

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.EWalletVerified,
            EntityType = "CustomerEWallet",
            EntityId = ewallet.Id,
            NewValuesJson = AuditHelper.ToJson(new { customerId, provider = ewallet.WalletProvider, status = "Active" })
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation("E-wallet verified and activated. EWalletId={Id} Provider={Provider}",
            ewallet.Id, ewallet.WalletProvider);

        return Result.Success(MapToDto(ewallet));
    }

    // ── Unlink Wallet ───────────────────────────────────────────────────────

    public async Task<Result> UnlinkWalletAsync(int customerId, int ewalletId, int userId)
    {
        var ewallet = await _db.CustomerEWallets.FirstOrDefaultAsync(w =>
            w.Id == ewalletId && w.CustomerId == customerId);

        if (ewallet is null)
            return Result.Failure("Liên kết ví không tồn tại.");

        if (ewallet.Status == EWalletLinkStatus.Unlinked)
            return Result.Failure("Ví đã được hủy liên kết trước đó.");

        ewallet.Status = EWalletLinkStatus.Unlinked;
        ewallet.UnlinkedAt = DateTime.UtcNow;
        ewallet.WalletToken = null; // Invalidate token

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.EWalletUnlinked,
            EntityType = "CustomerEWallet",
            EntityId = ewalletId,
            NewValuesJson = AuditHelper.ToJson(new { customerId, provider = ewallet.WalletProvider })
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation("E-wallet unlinked. EWalletId={Id} Provider={Provider}", ewalletId, ewallet.WalletProvider);

        return Result.Success();
    }

    // ── Get by Customer ─────────────────────────────────────────────────────

    public async Task<List<CustomerEWalletDto>> GetByCustomerIdAsync(int customerId)
    {
        return await _db.CustomerEWallets
            .Where(w => w.CustomerId == customerId && w.Status != EWalletLinkStatus.Unlinked)
            .OrderByDescending(w => w.LinkedAt)
            .Select(w => MapToDto(w))
            .ToListAsync();
    }

    // ── Pay from E-Wallet ───────────────────────────────────────────────────

    public async Task<Result<EWalletPaymentResultDto>> PayFromWalletAsync(int loanId, EWalletPaymentRequest req, int userId)
    {
        // Validate amount
        if (req.Amount <= 0)
            return Result.Failure<EWalletPaymentResultDto>("Số tiền thanh toán phải lớn hơn 0.");

        if (req.Amount > EWalletLimit.MaxTransactionAmount)
            return Result.Failure<EWalletPaymentResultDto>($"Số tiền vượt quá giới hạn giao dịch ({EWalletLimit.MaxTransactionAmount:N0} VND).");

        // Validate ewallet
        var ewallet = await _db.CustomerEWallets.FindAsync(req.EWalletId);
        if (ewallet is null)
            return Result.Failure<EWalletPaymentResultDto>("Ví điện tử không tồn tại.");
        if (ewallet.Status != EWalletLinkStatus.Active)
            return Result.Failure<EWalletPaymentResultDto>("Ví điện tử chưa được kích hoạt hoặc đã bị hủy.");
        if (string.IsNullOrWhiteSpace(ewallet.WalletToken))
            return Result.Failure<EWalletPaymentResultDto>("Ví điện tử chưa có token binding. Vui lòng liên kết lại.");

        // Validate loan belongs to customer
        var loan = await _db.Loans.FindAsync(loanId);
        if (loan is null)
            return Result.Failure<EWalletPaymentResultDto>("Khoản vay không tồn tại.");
        if (loan.CustomerId != ewallet.CustomerId)
            return Result.Failure<EWalletPaymentResultDto>("Ví không thuộc khách hàng của khoản vay.");

        // Charge from wallet
        EWalletChargeResult chargeResult;
        try
        {
            chargeResult = await _provider.ChargeAsync(
                ewallet.WalletToken!,
                req.Amount,
                $"Thanh toán khoản vay {loan.LoanAccountNo}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "E-wallet charge failed. LoanId={LoanId} EWalletId={EWalletId}", loanId, req.EWalletId);
            return Result.Failure<EWalletPaymentResultDto>($"Trừ tiền ví thất bại: {ex.Message}");
        }

        if (!chargeResult.IsSuccess)
            return Result.Failure<EWalletPaymentResultDto>($"Thanh toán thất bại: {chargeResult.ErrorMessage}");

        // Record payment using existing payment service
        var paymentReq = new RecordPaymentRequest
        {
            PaymentDate = _businessClock.TodayBusinessDate(),
            Amount = req.Amount,
            PaymentMethod = PaymentMethod.EWallet,
            Remarks = $"E-Wallet ({ewallet.WalletProvider}) — TxId: {chargeResult.TransactionId}",
            ExternalReferenceId = chargeResult.TransactionId
        };

        var paymentResult = await _paymentService.RecordPaymentAsync(loanId, paymentReq, userId);

        if (!paymentResult.IsSuccess)
        {
            _logger.LogError("E-wallet charged but payment recording failed. TxId={TxId} Error={Error}",
                chargeResult.TransactionId, paymentResult.Error);
            return Result.Failure<EWalletPaymentResultDto>($"Đã trừ tiền ví nhưng ghi nhận thanh toán thất bại: {paymentResult.Error}. Mã giao dịch: {chargeResult.TransactionId}");
        }

        ewallet.LastUsedAt = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.EWalletPayment,
            EntityType = "Loan",
            EntityId = loanId,
            NewValuesJson = AuditHelper.ToJson(new
            {
                ewalletId = ewallet.Id,
                provider = ewallet.WalletProvider,
                amount = req.Amount,
                transactionId = chargeResult.TransactionId
            })
        });

        await _db.SaveChangesAsync();

        return Result.Success(new EWalletPaymentResultDto
        {
            TransactionId = chargeResult.TransactionId,
            Status = chargeResult.Status,
            Amount = req.Amount,
            WalletProvider = ewallet.WalletProvider,
            ProcessedAt = chargeResult.ProcessedAt
        });
    }

    // ── Disburse to E-Wallet ────────────────────────────────────────────────

    public async Task<Result<EWalletDisbursementResultDto>> DisburseToWalletAsync(int loanId, EWalletDisbursementRequest req, int userId)
    {
        // Validate ewallet
        var ewallet = await _db.CustomerEWallets.FindAsync(req.EWalletId);
        if (ewallet is null)
            return Result.Failure<EWalletDisbursementResultDto>("Ví điện tử không tồn tại.");
        if (ewallet.Status != EWalletLinkStatus.Active)
            return Result.Failure<EWalletDisbursementResultDto>("Ví điện tử chưa được kích hoạt.");
        if (string.IsNullOrWhiteSpace(ewallet.WalletToken))
            return Result.Failure<EWalletDisbursementResultDto>("Ví chưa có token binding.");

        // Validate loan
        var loan = await _db.Loans
            .Include(l => l.Customer)
            .FirstOrDefaultAsync(l => l.Id == loanId);

        if (loan is null)
            return Result.Failure<EWalletDisbursementResultDto>("Khoản vay không tồn tại.");
        if (loan.CustomerId != ewallet.CustomerId)
            return Result.Failure<EWalletDisbursementResultDto>("Ví không thuộc khách hàng của khoản vay.");
        if (loan.Status != LoanStatus.PendingDisbursement)
            return Result.Failure<EWalletDisbursementResultDto>("Khoản vay không ở trạng thái chờ giải ngân.");

        // Check existing disbursement
        if (await _db.LoanDisbursements.AnyAsync(d => d.LoanId == loanId))
            return Result.Failure<EWalletDisbursementResultDto>("Khoản vay đã được giải ngân.");

        // Amount limit
        if (loan.ApprovedAmount > EWalletLimit.MaxTransactionAmount)
            return Result.Failure<EWalletDisbursementResultDto>($"Số tiền giải ngân ({loan.ApprovedAmount:N0}) vượt giới hạn ví ({EWalletLimit.MaxTransactionAmount:N0} VND).");

        // Payout to wallet
        EWalletChargeResult payoutResult;
        try
        {
            payoutResult = await _provider.PayoutAsync(
                ewallet.WalletToken!,
                loan.ApprovedAmount,
                $"Giải ngân khoản vay {loan.LoanAccountNo}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "E-wallet payout failed. LoanId={LoanId} EWalletId={EWalletId}", loanId, req.EWalletId);
            return Result.Failure<EWalletDisbursementResultDto>($"Chuyển tiền vào ví thất bại: {ex.Message}");
        }

        if (!payoutResult.IsSuccess)
            return Result.Failure<EWalletDisbursementResultDto>($"Giải ngân thất bại: {payoutResult.ErrorMessage}");

        // Record disbursement
        var disbursement = new LoanDisbursement
        {
            LoanId = loanId,
            DisbursedAmount = loan.ApprovedAmount,
            DisbursementDate = _businessClock.TodayBusinessDate(),
            RecordedByUserId = userId,
            DisbursementMethod = "EWallet",
            DisbursementStatus = DisbursementStatus.Completed,
            NapasTransactionId = payoutResult.TransactionId,
            GatewayConfirmedAt = payoutResult.ProcessedAt,
            CreatedAt = _businessClock.UtcNow
        };
        _db.LoanDisbursements.Add(disbursement);

        ewallet.LastUsedAt = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = userId,
            ActionCode = AuditActions.EWalletDisbursement,
            EntityType = "LoanDisbursement",
            EntityId = 0,
            NewValuesJson = AuditHelper.ToJson(new
            {
                loanId,
                ewalletId = ewallet.Id,
                provider = ewallet.WalletProvider,
                amount = loan.ApprovedAmount,
                transactionId = payoutResult.TransactionId
            })
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation("E-wallet disbursement completed. LoanId={LoanId} Provider={Provider} Amount={Amount:N0} TxId={TxId}",
            loanId, ewallet.WalletProvider, loan.ApprovedAmount, payoutResult.TransactionId);

        return Result.Success(new EWalletDisbursementResultDto
        {
            TransactionId = payoutResult.TransactionId,
            Status = payoutResult.Status,
            Amount = loan.ApprovedAmount,
            WalletProvider = ewallet.WalletProvider,
            ProcessedAt = payoutResult.ProcessedAt,
            DisbursementId = disbursement.Id
        });
    }

    // ── Mapping ─────────────────────────────────────────────────────────────

    private static CustomerEWalletDto MapToDto(CustomerEWallet w) => new()
    {
        Id = w.Id,
        CustomerId = w.CustomerId,
        WalletProvider = w.WalletProvider,
        WalletAccountId = w.WalletAccountId,
        WalletUserName = w.WalletUserName,
        Status = w.Status,
        LinkedAt = w.LinkedAt,
        VerifiedAt = w.VerifiedAt,
        LastUsedAt = w.LastUsedAt
    };
}
