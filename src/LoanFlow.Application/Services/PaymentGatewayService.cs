using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

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
