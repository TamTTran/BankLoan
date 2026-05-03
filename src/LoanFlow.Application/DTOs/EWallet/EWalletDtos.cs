namespace LoanFlow.Application.DTOs.EWallet;

// ── Requests ────────────────────────────────────────────────────────────────

public class LinkEWalletRequest
{
    /// <summary>MoMo | VNPay</summary>
    public string WalletProvider { get; set; } = default!;

    /// <summary>Số điện thoại hoặc Wallet ID</summary>
    public string WalletAccountId { get; set; } = default!;
}

public class VerifyEWalletOtpRequest
{
    /// <summary>ID của CustomerEWallet đang PendingVerification</summary>
    public int LinkingId { get; set; }

    /// <summary>OTP code từ ví điện tử</summary>
    public string OtpCode { get; set; } = default!;
}

public class EWalletPaymentRequest
{
    /// <summary>ID ví điện tử đã liên kết (Active)</summary>
    public int EWalletId { get; set; }

    /// <summary>Số tiền thanh toán (VND). Tối đa 100.000.000</summary>
    public decimal Amount { get; set; }
}

public class EWalletDisbursementRequest
{
    /// <summary>ID ví điện tử đã liên kết (Active)</summary>
    public int EWalletId { get; set; }
}

// ── Responses ───────────────────────────────────────────────────────────────

public class EWalletTransactionQueryDto
{
    public string TransactionId { get; set; } = default!;
    public string Status { get; set; } = default!;
    public decimal Amount { get; set; }
    public string WalletProvider { get; set; } = default!;
    public DateTime ProcessedAt { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CustomerEWalletDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string WalletProvider { get; set; } = default!;
    public string WalletAccountId { get; set; } = default!;
    public string? WalletUserName { get; set; }
    public string Status { get; set; } = default!;
    public DateTime LinkedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public class EWalletPaymentResultDto
{
    public string TransactionId { get; set; } = default!;
    public string Status { get; set; } = default!;
    public decimal Amount { get; set; }
    public string WalletProvider { get; set; } = default!;
    public DateTime ProcessedAt { get; set; }
}

public class EWalletDisbursementResultDto
{
    public string TransactionId { get; set; } = default!;
    public string Status { get; set; } = default!;
    public decimal Amount { get; set; }
    public string WalletProvider { get; set; } = default!;
    public DateTime ProcessedAt { get; set; }
    public int DisbursementId { get; set; }
}
