using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

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
