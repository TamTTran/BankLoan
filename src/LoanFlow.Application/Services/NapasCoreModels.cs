namespace LoanFlow.Application.Services;

// ══════════════════════════════════════════════════════════════════════════════
// NAPAS Core Models — Transaction Query, Merchant, Reconciliation, QR Code
// Tham chiếu: NAPAS Pay 24/7 API Spec v2.1 + VietQR EMV QRCPS v1.0
// ══════════════════════════════════════════════════════════════════════════════

// ── Transaction Query ─────────────────────────────────────────────────────────

public class NapasTransactionDetail
{
    public string  TransactionId   { get; set; } = default!;
    public string  ReferenceId     { get; set; } = default!;
    /// <summary>SUCCESS | FAILED | PENDING | REVERSED</summary>
    public string  Status          { get; set; } = default!;
    public decimal Amount          { get; set; }
    public string  Currency        { get; set; } = "VND";
    public string  BankCode        { get; set; } = default!;
    public string  AccountNumber   { get; set; } = default!;
    public string? AccountName     { get; set; }
    public string? Remark          { get; set; }
    /// <summary>IBFT (chuyển liên ngân hàng) | INTRABANK | QR_PAYMENT</summary>
    public string  TransactionType { get; set; } = "IBFT";
    public DateTime ProcessedAt   { get; set; }
    public string? ErrorCode      { get; set; }
    public string? ErrorMessage   { get; set; }
    /// <summary>Fee tính bằng VND (nếu có)</summary>
    public decimal? FeeAmount     { get; set; }
}

public class NapasTransactionSearchRequest
{
    public DateOnly? FromDate       { get; set; }
    public DateOnly? ToDate         { get; set; }
    /// <summary>SUCCESS | FAILED | PENDING — null = tất cả</summary>
    public string?  Status          { get; set; }
    /// <summary>IBFT | QR_PAYMENT — null = tất cả</summary>
    public string?  TransactionType { get; set; }
    public int      Page            { get; set; } = 1;
    public int      PageSize        { get; set; } = 20;
}

// ── Merchant Info ─────────────────────────────────────────────────────────────

public class NapasMerchantInfo
{
    public string   MerchantId       { get; set; } = default!;
    public string   MerchantName     { get; set; } = default!;
    public string   MerchantCode     { get; set; } = default!;
    /// <summary>MCC — Merchant Category Code (e.g. "6022" = State chartered banks)</summary>
    public string   CategoryCode     { get; set; } = default!;
    public string   CategoryName     { get; set; } = default!;
    /// <summary>ACTIVE | SUSPENDED | PENDING</summary>
    public string   Status           { get; set; } = default!;
    public string   BankCode         { get; set; } = default!;
    public string   SettlementAccount { get; set; } = default!;
    public string?  ContactEmail     { get; set; }
    public string?  ContactPhone     { get; set; }
    public DateTime RegisteredAt     { get; set; }
    public DateTime? LastUpdatedAt   { get; set; }
}

public class NapasMerchantSettings
{
    /// <summary>Hạn mức 1 giao dịch (VND)</summary>
    public decimal SingleTransactionLimit { get; set; }
    /// <summary>Hạn mức tổng trong ngày (VND)</summary>
    public decimal DailyTransactionLimit  { get; set; }
    /// <summary>Hạn mức tháng (VND)</summary>
    public decimal MonthlyTransactionLimit { get; set; }
    /// <summary>Kênh giao dịch được phép: IBFT, QR, etc.</summary>
    public IEnumerable<string> EnabledChannels { get; set; } = [];
    /// <summary>Danh sách mã ngân hàng được phép nhận tiền</summary>
    public IEnumerable<string> AllowedBankCodes { get; set; } = [];
    public bool  AutoSettlement     { get; set; }
    public string SettlementTime    { get; set; } = "17:00";
    public bool  CallbackEnabled    { get; set; }
    public string? CallbackUrl      { get; set; }
}

// ── Reconciliation ────────────────────────────────────────────────────────────

public class NapasRecoFileInfo
{
    public DateOnly BusinessDate { get; set; }
    /// <summary>settlement | dispute | reversal</summary>
    public string   FileType     { get; set; } = default!;
    public string   FileName     { get; set; } = default!;
    public long     SizeBytes    { get; set; }
    public int      RecordCount  { get; set; }
    public DateTime GeneratedAt  { get; set; }
}

public class NapasRecoRecord
{
    public string   TransactionId  { get; set; } = default!;
    public string   ReferenceId    { get; set; } = default!;
    public decimal  Amount         { get; set; }
    public string   Status         { get; set; } = default!;
    public string   BankCode       { get; set; } = default!;
    public string   AccountNumber  { get; set; } = default!;
    public DateTime ProcessedAt    { get; set; }
    public string?  Remark         { get; set; }
    /// <summary>Fee NAPAS tính trên giao dịch</summary>
    public decimal  FeeAmount      { get; set; }
}

public class NapasRecoFileContent
{
    public DateOnly              BusinessDate  { get; set; }
    public string                FileType      { get; set; } = default!;
    public string                RawCsv        { get; set; } = default!;
    public IEnumerable<NapasRecoRecord> Records { get; set; } = [];
}

public class NapasRecoMatchResult
{
    public DateOnly  BusinessDate        { get; set; }
    public int       TotalNapasRecords   { get; set; }
    public int       TotalInternalRecords { get; set; }
    public int       MatchedCount        { get; set; }
    public int       CreatedExceptions   { get; set; }
    public decimal   TotalNapasAmount    { get; set; }
    public decimal   TotalInternalAmount { get; set; }
    public decimal   AmountDifference    { get; set; }
    /// <summary>NAPAS records không tìm thấy trong DB</summary>
    public IEnumerable<NapasRecoRecord>    UnmatchedNapasRecords    { get; set; } = [];
    /// <summary>DB records không có trong file NAPAS</summary>
    public IEnumerable<UnmatchedInternal>  UnmatchedInternalRecords { get; set; } = [];
    /// <summary>Records khớp được nhưng số tiền lệch</summary>
    public IEnumerable<RecoAmountMismatch> AmountMismatches         { get; set; } = [];
}

public class UnmatchedInternal
{
    public int      DisbursementId  { get; set; }
    public string   NapasReferenceId { get; set; } = default!;
    public string?  NapasTransactionId { get; set; }
    public decimal  Amount          { get; set; }
    public string   DisbursementStatus { get; set; } = default!;
    public DateTime CreatedAt       { get; set; }
}

public class RecoAmountMismatch
{
    public string   ReferenceId     { get; set; } = default!;
    public decimal  NapasAmount     { get; set; }
    public decimal  InternalAmount  { get; set; }
    public decimal  Difference      { get; set; }
}

// ── QR Code (VietQR — EMV QRCPS) ─────────────────────────────────────────────

public class NapasQrRequest
{
    /// <summary>Nếu có LoanId → auto fill AccountNumber, AccountName từ Loan</summary>
    public int?    LoanId         { get; set; }
    public decimal Amount         { get; set; }
    public string  Description    { get; set; } = default!;
    /// <summary>Override nếu khác tài khoản settlement mặc định</summary>
    public string? BankCode       { get; set; }
    public string? AccountNumber  { get; set; }
    public string? AccountName    { get; set; }
    /// <summary>Thời gian hết hạn (phút). 0 = không hết hạn. Default: 30</summary>
    public int     ExpiryMinutes  { get; set; } = 30;
}

public class NapasQrResponse
{
    /// <summary>Chuỗi QR theo chuẩn EMV QRCPS (paste vào app bank để quét)</summary>
    public string   QrString      { get; set; } = default!;
    /// <summary>QR image PNG dạng Base64 (hiển thị thẳng trong UI)</summary>
    public string   QrImageBase64 { get; set; } = default!;
    /// <summary>ReferenceId nhúng vào QR để trace</summary>
    public string   ReferenceId   { get; set; } = default!;
    public decimal  Amount        { get; set; }
    public DateTime GeneratedAt   { get; set; }
    public DateTime? ExpiresAt    { get; set; }
    public string   BankCode      { get; set; } = default!;
    public string   AccountNumber { get; set; } = default!;
    public string?  AccountName   { get; set; }
    /// <summary>Link VietQR API để render ảnh QR online (nếu dùng vietqr.io)</summary>
    public string   VietQrUrl     { get; set; } = default!;
}

public class NapasQrDecoded
{
    public string?  MerchantName   { get; set; }
    public string   BankCode       { get; set; } = default!;
    public string   AccountNumber  { get; set; } = default!;
    public decimal? Amount         { get; set; }
    public string?  Description    { get; set; }
    public string?  ReferenceId    { get; set; }
    public bool     IsValid        { get; set; }
    public string?  ValidationError { get; set; }
}
