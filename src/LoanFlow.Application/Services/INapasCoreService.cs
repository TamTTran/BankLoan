using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

/// <summary>
/// NAPAS Core Service — Transaction Query, Merchant Info, Reconciliation, QR Code.
/// Implemented by:
///   - MockNapasCoreService  : dev/test (không cần sandbox credentials)
///   - NapasCoreService      : production (gọi NAPAS REST API)
/// </summary>
public interface INapasCoreService
{
    // ── Transaction Query ─────────────────────────────────────────────────────

    /// <summary>Lấy chi tiết giao dịch theo NAPAS Transaction ID</summary>
    Task<Result<NapasTransactionDetail>> GetTransactionAsync(string napasTransactionId);

    /// <summary>Lấy chi tiết giao dịch theo ReferenceId hệ thống (LF-...)</summary>
    Task<Result<NapasTransactionDetail>> GetTransactionByReferenceAsync(string referenceId);

    /// <summary>Tìm kiếm giao dịch theo ngày / trạng thái / loại</summary>
    Task<Result<PagedResult<NapasTransactionDetail>>> SearchTransactionsAsync(NapasTransactionSearchRequest request);

    // ── Merchant Info ─────────────────────────────────────────────────────────

    /// <summary>Thông tin Merchant đã đăng ký trên NAPAS</summary>
    Task<Result<NapasMerchantInfo>> GetMerchantInfoAsync();

    /// <summary>Cài đặt hiện tại của Merchant (hạn mức, kênh, callback URL)</summary>
    Task<Result<NapasMerchantSettings>> GetMerchantSettingsAsync();

    // ── Reconciliation ────────────────────────────────────────────────────────

    /// <summary>Danh sách file đối soát NAPAS tạo ra cho ngày chỉ định</summary>
    Task<Result<IEnumerable<NapasRecoFileInfo>>> ListRecoFilesAsync(DateOnly date);

    /// <summary>Tải nội dung file đối soát (CSV parse thành records)</summary>
    Task<Result<NapasRecoFileContent>> DownloadRecoFileAsync(DateOnly date, string fileType);

    /// <summary>
    /// Khớp lệnh file NAPAS vs giao dịch nội bộ cho ngày chỉ định.
    /// Unmatched records → tạo ReconciliationException entity (ExceptionType = DisbursementMismatch).
    /// </summary>
    Task<Result<NapasRecoMatchResult>> MatchRecoFileAsync(DateOnly date, int requestingUserId);

    // ── QR Code (VietQR EMV QRCPS offline) ───────────────────────────────────

    /// <summary>Tạo VietQR code (EMV QRCPS) — offline, không cần gọi NAPAS API</summary>
    Task<Result<NapasQrResponse>> GenerateQrAsync(NapasQrRequest request);

    /// <summary>Giải mã QR string theo chuẩn EMV QRCPS</summary>
    Task<Result<NapasQrDecoded>> DecodeQrAsync(string qrString);

    /// <summary>
    /// Lấy QR code cho khoản vay (dùng VirtualAccountNo của Loan).
    /// Nếu Loan dùng virtual account → amount=0 (khách nhập), nếu có installment đến hạn → pre-fill amount.
    /// </summary>
    Task<Result<NapasQrResponse>> GetOrCreateLoanQrAsync(int loanId);
}
