using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.EWallet;

namespace LoanFlow.Application.Services;

public interface IEWalletService
{
    /// <summary>Khởi tạo liên kết ví → gửi OTP cho khách qua ví</summary>
    Task<Result<CustomerEWalletDto>> LinkWalletAsync(int customerId, LinkEWalletRequest req, int userId);

    /// <summary>Xác thực OTP liên kết ví → status Active</summary>
    Task<Result<CustomerEWalletDto>> VerifyLinkingOtpAsync(int customerId, VerifyEWalletOtpRequest req, int userId);

    /// <summary>Hủy liên kết ví (soft-delete)</summary>
    Task<Result> UnlinkWalletAsync(int customerId, int ewalletId, int userId);

    /// <summary>Lấy danh sách ví đã liên kết của khách hàng</summary>
    Task<List<CustomerEWalletDto>> GetByCustomerIdAsync(int customerId);

    /// <summary>Thanh toán khoản vay bằng ví điện tử</summary>
    Task<Result<EWalletPaymentResultDto>> PayFromWalletAsync(int loanId, EWalletPaymentRequest req, int userId);

    /// <summary>Giải ngân vào ví điện tử</summary>
    Task<Result<EWalletDisbursementResultDto>> DisburseToWalletAsync(int loanId, EWalletDisbursementRequest req, int userId);

    /// <summary>Truy vấn trạng thái giao dịch ví điện tử từ provider</summary>
    Task<Result<EWalletTransactionQueryDto>> QueryTransactionAsync(string provider, string transactionId);
}
