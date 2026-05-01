using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

// ────────────────────────────────────────────────────────────────────────────
// Core interface — implemented by both real NAPAS and Mock providers
// ────────────────────────────────────────────────────────────────────────────

public interface IPaymentGatewayService
{
    /// <summary>
    /// [SYNC] Gửi lệnh chuyển tiền giải ngân đến tài khoản ngân hàng của khách.
    /// Với NAPAS thực: kết quả trả về ngay trong ~2–5 giây (Napas Pay 24/7).
    /// NapasReferenceId trong response dùng để track callback async.
    /// </summary>
    Task<Result<PaymentGatewayResponse>> TransferToCustomerAsync(
        string bankCode, string accountNumber, decimal amount, string remark);

    /// <summary>
    /// [ASYNC CALLBACK] Xử lý callback từ NAPAS xác nhận kết quả giao dịch.
    /// NAPAS sẽ POST về endpoint /api/napas/callback sau khi xử lý xong.
    /// </summary>
    Task<Result<CallbackHandleResult>> HandleCallbackAsync(NapasCallbackPayload payload);

    /// <summary>
    /// [QUERY] Chủ động query trạng thái giao dịch từ NAPAS.
    /// Dùng trong trường hợp callback không đến sau thời gian chờ.
    /// </summary>
    Task<Result<PaymentGatewayResponse>> QueryTransactionStatusAsync(string napasReferenceId);
}

// ────────────────────────────────────────────────────────────────────────────
// DTOs
// ────────────────────────────────────────────────────────────────────────────

public class PaymentGatewayResponse
{
    /// <summary>Mã giao dịch từ NAPAS (transactionId)</summary>
    public string TransactionId { get; set; } = default!;
    /// <summary>ReferenceId do hệ thống tạo (LF-yyyyMMddHHmmss-guid), dùng để match callback</summary>
    public string ReferenceId { get; set; } = default!;
    public DateTime ProcessedAt { get; set; }
    public string OriginalBankCode { get; set; } = default!;
    public string OriginalAccountNumber { get; set; } = default!;
    /// <summary>true nếu NAPAS xác nhận ngay (sync), false nếu đang chờ async callback</summary>
    public bool IsConfirmedImmediately { get; set; }
}

/// <summary>
/// Payload từ NAPAS khi callback về webhook /api/napas/callback
/// </summary>
public class NapasCallbackPayload
{
    /// <summary>ReferenceId do hệ thống tạo, gửi lên NAPAS khi transfer, NAPAS echo lại</summary>
    public string ReferenceId { get; set; } = default!;
    /// <summary>Mã giao dịch NAPAS tạo ra</summary>
    public string NapasTransactionId { get; set; } = default!;
    /// <summary>SUCCESS | FAILED</summary>
    public string Status { get; set; } = default!;
    /// <summary>Mã lỗi NAPAS (nếu Failed)</summary>
    public string? ErrorCode { get; set; }
    /// <summary>Mô tả lỗi NAPAS (nếu Failed)</summary>
    public string? ErrorMessage { get; set; }
    public DateTime ProcessedAt { get; set; }
    /// <summary>Số tài khoản thụ hưởng (echo từ NAPAS để verify)</summary>
    public string? BeneficiaryAccount { get; set; }
    /// <summary>Số tiền (echo từ NAPAS để verify)</summary>
    public decimal? Amount { get; set; }
}

public class CallbackHandleResult
{
    public int DisbursementId { get; set; }
    public string NewStatus { get; set; } = default!;
    public bool IsSuccess { get; set; }
}

