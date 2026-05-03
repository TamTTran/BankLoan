namespace LoanFlow.Domain.Entities;

/// <summary>
/// Liên kết ví điện tử của khách hàng.
/// Hỗ trợ cả thanh toán khoản vay (payment in) và nhận giải ngân (payout).
/// Giới hạn giao dịch: 100.000.000 VND / lần.
/// </summary>
public class CustomerEWallet
{
    public int Id { get; set; }
    public int CustomerId { get; set; }

    /// <summary>MoMo | VNPay</summary>
    public string WalletProvider { get; set; } = default!;

    /// <summary>Số điện thoại hoặc Wallet ID liên kết</summary>
    public string WalletAccountId { get; set; } = default!;

    /// <summary>Tên hiển thị từ ví (trả về khi verify OTP)</summary>
    public string? WalletUserName { get; set; }

    /// <summary>Token binding đã mã hóa (dùng để charge/payout mà không cần OTP lại)</summary>
    public string? WalletToken { get; set; }

    /// <summary>PendingVerification | Active | Suspended | Unlinked</summary>
    public string Status { get; set; } = "PendingVerification";

    /// <summary>OTP nonce dùng trong quá trình verify linking (xóa sau khi verify xong)</summary>
    public string? OtpNonce { get; set; }

    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAt { get; set; }
    public DateTime? UnlinkedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    public int LinkedByUserId { get; set; }

    // ── Navigation ──────────────────────────────────────────────────────
    public Customer Customer { get; set; } = default!;
    public User LinkedBy { get; set; } = default!;
}
