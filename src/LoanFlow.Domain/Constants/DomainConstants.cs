namespace LoanFlow.Domain.Constants;

public static class Roles
{
    public const string Admin = "Admin";
    public const string LoanOfficer = "LoanOfficer";
    public const string Approver = "Approver";
    public const string Collector = "Collector";
    public const string Manager = "Manager";
    public static readonly string[] All = [Admin, LoanOfficer, Approver, Collector, Manager];
}

public static class ApplicationStatus
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public static class LoanStatus
{
    public const string PendingDisbursement = "PendingDisbursement";
    public const string Active = "Active";
    public const string Overdue = "Overdue";
    public const string Closed = "Closed";
    // ── Phase 2 ────────────
    public const string Restructured = "Restructured";  // Đã cơ cấu lại, tiếp tục trả nợ
    public const string WrittenOff = "WrittenOff";       // Đã xóa nợ, không có khả năng thu hồi
}

public static class InstallmentStatus
{
    public const string Unpaid = "Unpaid";
    public const string Partial = "Partial";
    public const string Paid = "Paid";
    public const string Overdue = "Overdue";
}

public static class PaymentMethod
{
    public const string Cash = "Cash";
    public const string BankTransfer = "BankTransfer";
    public const string SuspenseAccount = "SuspenseAccount";
}

// ── NAPAS Disbursement Gateway Status ───────────────────────────────────────
/// <summary>
/// Trạng thái giải ngân qua NAPAS. Chỉ áp dụng khi DisbursementMethod = BankTransfer.
/// </summary>
public static class DisbursementStatus
{
    /// <summary>Không áp dụng (giải ngân tiền mặt)</summary>
    public const string NotApplicable = "NotApplicable";
    /// <summary>Lệnh đã gửi NAPAS, chờ xác nhận async</summary>
    public const string Pending = "Pending";
    /// <summary>NAPAS đã xác nhận chuyển tiền thành công</summary>
    public const string Completed = "Completed";
    /// <summary>NAPAS báo lỗi hoặc hết thời gian chờ</summary>
    public const string Failed = "Failed";
}

// ── Mã ngân hàng chuẩn NAPAS (SWIFT/BIC nội địa) ───────────────────────────
/// <summary>
/// Mã ngân hàng chuẩn theo danh mục NAPAS Interbank.
/// Tham chiếu: https://www.napas.com.vn
/// </summary>
public static class NapasBankCode
{
    // Big 4 State-owned
    public const string VCB  = "VCB";   // Vietcombank
    public const string VTB  = "VTB";   // VietinBank
    public const string BID  = "BID";   // BIDV
    public const string AGR  = "AGR";   // Agribank

    // Joint-stock commercial banks
    public const string ACB  = "ACB";   // Asia Commercial Bank
    public const string TCB  = "TCB";   // Techcombank
    public const string MBB  = "MBB";   // MB Bank
    public const string VPB  = "VPB";   // VPBank
    public const string STB  = "STB";   // Sacombank
    public const string TPB  = "TPB";   // TPBank
    public const string HDB  = "HDB";   // HDBank
    public const string OCB  = "OCB";   // Orient Commercial Bank
    public const string MSB  = "MSB";   // Maritime Bank
    public const string SHB  = "SHB";   // SHBank
    public const string VIB  = "VIB";   // Vietnam International Bank
    public const string EIB  = "EIB";   // Eximbank
    public const string NAB  = "NAB";   // Nam A Bank
    public const string SCB  = "SCB";   // Saigon Commercial Bank
    public const string BAB  = "BAB";   // Bac A Bank
    public const string LPB  = "LPB";   // LienVietPostBank
    public const string KLB  = "KLB";   // Kien Long Bank
    public const string BVB  = "BVB";   // BaoViet Bank
    public const string PVB  = "PVB";   // PVcomBank
    public const string IVB  = "IVB";   // IndoVina Bank
    public const string SEAB = "SEAB";  // SeABank
    public const string ABB  = "ABB";   // AnBinhBank

    // Digital / Neo banks
    public const string TIMO = "TIMO";  // Timo (backed by VPBank)
    public const string CAKE = "CAKE";  // Cake (backed by VPBank)
    public const string TNEX = "TNEX";  // TNEX (backed by MSB)

    // Foreign banks (branches in VN)
    public const string HSBC = "HSBC";  // HSBC Vietnam
    public const string SCB_STANDARD = "SCBL"; // Standard Chartered VN
    public const string CITI = "CITI";  // Citibank VN

    public static readonly IReadOnlySet<string> ValidCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        VCB, VTB, BID, AGR,
        ACB, TCB, MBB, VPB, STB, TPB, HDB, OCB, MSB, SHB, VIB, EIB, NAB, SCB, BAB, LPB, KLB, BVB, PVB, IVB, SEAB, ABB,
        TIMO, CAKE, TNEX,
        HSBC, SCB_STANDARD, CITI
    };

    public static bool IsValid(string bankCode) =>
        !string.IsNullOrWhiteSpace(bankCode) && ValidCodes.Contains(bankCode);
}

public static class RepaymentFrequency
{
    public const string Monthly = "Monthly";
}

// ── Loan Product Type (inspired by VPBank, Techcombank, VIB, TPBank, BIDV) ──
public static class ProductType
{
    public const string PersonalUnsecured = "PersonalUnsecured";   // Vay tín chấp
    public const string PersonalSecured = "PersonalSecured";       // Vay thế chấp cá nhân
    public const string Mortgage = "Mortgage";                      // Vay mua nhà
    public const string AutoLoan = "AutoLoan";                      // Vay mua xe
    public const string BusinessLoan = "BusinessLoan";              // Vay kinh doanh
    public const string Overdraft = "Overdraft";                    // Thấu chi

    public static readonly string[] All =
        [PersonalUnsecured, PersonalSecured, Mortgage, AutoLoan, BusinessLoan, Overdraft];
}

// ── Interest Calculation Method ─────────────────────────────────────────────
public static class InterestMethod
{
    public const string Flat = "Flat";                              // Lãi cố định trên gốc ban đầu
    public const string ReducingBalance = "ReducingBalance";        // Lãi trên dư nợ giảm dần
}

// ── Collateral Types ────────────────────────────────────────────────────────
public static class CollateralType
{
    public const string RealEstate = "RealEstate";                  // Bất động sản / Sổ đỏ
    public const string Vehicle = "Vehicle";                        // Xe ô tô / xe máy
    public const string SavingsBook = "SavingsBook";                // Sổ tiết kiệm
    public const string OtherAsset = "OtherAsset";                  // Tài sản khác
}

// ── Collateral Status ───────────────────────────────────────────────────────
public static class CollateralStatus
{
    public const string Active = "Active";
    public const string Released = "Released";
    public const string Seized = "Seized";
}

// ── Credit Grade ────────────────────────────────────────────────────────────
public static class CreditGrade
{
    public const string A = "A";  // 80-100: Xuất sắc
    public const string B = "B";  // 60-79:  Tốt
    public const string C = "C";  // 40-59:  Trung bình
    public const string D = "D";  // 20-39:  Yếu
    public const string E = "E";  // 0-19:   Rất yếu

    public static string FromScore(int score) => score switch
    {
        >= 80 => A,
        >= 60 => B,
        >= 40 => C,
        >= 20 => D,
        _ => E
    };

    public static string Recommendation(string grade) => grade switch
    {
        A => "Approve",
        B => "Approve",
        C => "Review",
        D => "Reject",
        E => "Reject",
        _ => "Review"
    };
}

// ── Late Fee Status ─────────────────────────────────────────────────────────
public static class LateFeeStatus
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Waived = "Waived";
}

public static class DpdBucket
{
    public const string Current = "Current";
    public const string Dpd1To7 = "DPD1_7";
    public const string Dpd8To30 = "DPD8_30";
    public const string Dpd31Plus = "DPD31Plus";

    public static string Calculate(int dpd) => dpd switch
    {
        <= 0 => Current,
        >= 1 and <= 7 => Dpd1To7,
        >= 8 and <= 30 => Dpd8To30,
        _ => Dpd31Plus
    };
}

public static class AuditActions
{
    public const string CustomerCreated = "CUSTOMER_CREATED";
    public const string CustomerUpdated = "CUSTOMER_UPDATED";
    public const string ProductCreated = "PRODUCT_CREATED";
    public const string ProductUpdated = "PRODUCT_UPDATED";
    public const string ApplicationCreated = "APPLICATION_CREATED";
    public const string ApplicationUpdated = "APPLICATION_UPDATED";
    public const string ApplicationSubmitted = "APPLICATION_SUBMITTED";
    public const string ApplicationApproved = "APPLICATION_APPROVED";
    public const string ApplicationRejected = "APPLICATION_REJECTED";
    public const string LoanCreated = "LOAN_CREATED";
    public const string LoanDisbursed = "LOAN_DISBURSED";
    public const string PaymentRecorded = "PAYMENT_RECORDED";
    public const string CollectionNoteAdded = "COLLECTION_NOTE_ADDED";
    public const string LoanEarlySettled = "LOAN_EARLY_SETTLED";
    public const string LateFeeWaived = "LATE_FEE_WAIVED";
    public const string LateFeeWaiverRequested = "LATE_FEE_WAIVER_REQUESTED";
    public const string CollateralCreated = "COLLATERAL_CREATED";
    public const string CollateralUpdated = "COLLATERAL_UPDATED";
    public const string CreditCheckPerformed = "CREDIT_CHECK_PERFORMED";
    public const string CustomerAssigned = "CUSTOMER_ASSIGNED";
    public const string CareNoteAdded = "CARE_NOTE_ADDED";
    public const string DailyOverdueJobCompleted = "DAILY_OVERDUE_JOB_COMPLETED";
    public const string OverdueStatusUpdated = "OVERDUE_STATUS_UPDATED";

    // ── Wave 2: Reversal & Reconciliation ───────────────────────────────
    public const string ReversalRequested = "REVERSAL_REQUESTED";
    public const string ReversalApproved = "REVERSAL_APPROVED";
    public const string ReversalRejected = "REVERSAL_REJECTED";
    public const string PaymentReversed = "PAYMENT_REVERSED";
    public const string FeeAdjusted = "FEE_ADJUSTED";
    public const string ReconciliationRun = "RECONCILIATION_RUN";
    public const string ReconciliationExceptionResolved = "RECONCILIATION_EXCEPTION_RESOLVED";
    public const string EodCompleted = "EOD_COMPLETED";

    // ── Wave 3: Collections ─────────────────────────────────────────────
    public const string CollectionActionAdded = "COLLECTION_ACTION_ADDED";
    public const string CollectorAssigned = "COLLECTOR_ASSIGNED";
    public const string PromiseRecorded = "PROMISE_RECORDED";
    public const string PromiseBroken = "PROMISE_BROKEN";
    // ── Wave 4: Overpayment & Notifications ─────────────────────────────
    public const string SuspenseCreated = "SUSPENSE_CREATED";
    public const string SuspenseApplied = "SUSPENSE_APPLIED";
    public const string SuspenseRefunded = "SUSPENSE_REFUNDED";
    public const string NotificationSent = "NOTIFICATION_SENT";

    // ── Phase 2: Loan Lifecycle ────────────────────────────────
    public const string LoanRestructured = "LOAN_RESTRUCTURED";
    public const string LoanRescheduled = "LOAN_RESCHEDULED";
    public const string LoanWrittenOff = "LOAN_WRITTEN_OFF";
    public const string LoanRecoveryRecorded = "LOAN_RECOVERY_RECORDED";

    // ── NAPAS Gateway ───────────────────────────────────────────────
    public const string AutoDisbursementSuccess = "AUTO_DISBURSEMENT_SUCCESS";
    public const string AutoDisbursementFailed  = "AUTO_DISBURSEMENT_FAILED";
    public const string AutoDisbursementPending = "AUTO_DISBURSEMENT_PENDING"; // Awaiting NAPAS async callback
    public const string NapasCallbackReceived   = "NAPAS_CALLBACK_RECEIVED";
}

// ── Reversal Type ───────────────────────────────────────────────────────────
public static class ReversalType
{
    public const string PaymentReversal = "PaymentReversal";
    public const string DisbursementCorrection = "DisbursementCorrection";
    public const string FeeAdjustment = "FeeAdjustment";
}

// ── Reversal Status ─────────────────────────────────────────────────────────
public static class ReversalStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

// ── Reconciliation Status ───────────────────────────────────────────────────
public static class ReconciliationStatus
{
    public const string Open = "Open";
    public const string Resolved = "Resolved";
    public const string Acknowledged = "Acknowledged";
}

// ── Reconciliation Exception Type ───────────────────────────────────────────
public static class ReconciliationExceptionType
{
    public const string PaymentMismatch = "PaymentMismatch";
    public const string AllocationMismatch = "AllocationMismatch";
    public const string DisbursementMismatch = "DisbursementMismatch";
    public const string ReversalMismatch = "ReversalMismatch";
    public const string LateFeeMismatch = "LateFeeMismatch";
    public const string SuspenseMismatch = "SuspenseMismatch";
    public const string OutstandingMismatch = "OutstandingMismatch";
    public const string TrialBalanceMismatch = "TrialBalanceMismatch";
}

// ── Wave 3: Collection Workflow ─────────────────────────────────────────────

public static class CollectionActionType
{
    public const string Call = "Call";
    public const string Visit = "Visit";
    public const string SMS = "SMS";
    public const string Letter = "Letter";
    public const string Legal = "Legal";

    public static readonly string[] All = [Call, Visit, SMS, Letter, Legal];
}

public static class CollectionOutcome
{
    public const string Contacted = "Contacted";
    public const string NoAnswer = "NoAnswer";
    public const string PromiseToPay = "PromiseToPay";
    public const string Refused = "Refused";
    public const string Escalated = "Escalated";

    public static readonly string[] All = [Contacted, NoAnswer, PromiseToPay, Refused, Escalated];
}

public static class PromiseStatus
{
    public const string Pending = "Pending";
    public const string Kept = "Kept";
    public const string Broken = "Broken";
}

public static class CollectorAssignmentStatus
{
    public const string Active = "Active";
    public const string Completed = "Completed";
    public const string Transferred = "Transferred";
}

public static class CollectionPriority
{
    public const string High = "High";       // DPD31Plus
    public const string Medium = "Medium";   // DPD8_30
    public const string Low = "Low";         // DPD1_7

    public static string FromBucket(string bucket) => bucket switch
    {
        DpdBucket.Dpd31Plus => High,
        DpdBucket.Dpd8To30 => Medium,
        _ => Low
    };
}

// ── Customer Care Interaction Types ─────────────────────────────────────────
public static class InteractionType
{
    public const string Call = "Call";
    public const string Email = "Email";
    public const string Zalo = "Zalo";
    public const string SMS = "SMS";
    public const string Meeting = "Meeting";
    public const string Other = "Other";

    public static readonly string[] All = [Call, Email, Zalo, SMS, Meeting, Other];
}

// ── Lead Scoring Priority ───────────────────────────────────────────────────
public static class LeadPriority
{
    public const string Hot = "Hot";           // Score 70-100 — liên hệ ngay
    public const string Warm = "Warm";         // Score 40-69  — theo dõi sát
    public const string Cold = "Cold";         // Score 10-39  — nurture
    public const string Dormant = "Dormant";   // Score <10    — khách ngủ đông / overdue

    public static string FromScore(int score) => score switch
    {
        >= 70 => Hot,
        >= 40 => Warm,
        >= 10 => Cold,
        _ => Dormant
    };
}

// ── Wave 4: Overpayment & Notifications ──────────────────────────────────────

public static class SuspenseStatus
{
    public const string Pending = "Pending";
    public const string Applied = "Applied";
    public const string Refunded = "Refunded";
}

public static class NotificationType
{
    // Existing — generated by EOD
    public const string DueReminder    = "DueReminder";
    public const string OverdueReminder = "OverdueReminder";
    public const string PromiseDue     = "PromiseDue";
    public const string System         = "System";

    // Transaction events — generated on action
    public const string PaymentReceived        = "PaymentReceived";        // Thu tiền
    public const string LoanOpSubmitted        = "LoanOpSubmitted";        // Cơ cấu/Dời ngày/Xóa nợ gửi duyệt
    public const string EarlySettleSubmitted   = "EarlySettleSubmitted";   // Tất toán sớm gửi duyệt
    public const string BatchJobCompleted      = "BatchJobCompleted";      // EOD / Overdue job hoàn thành
    public const string ApprovalDecided        = "ApprovalDecided";        // Phê duyệt / Từ chối yêu cầu
    public const string ReversalRequested      = "ReversalRequested";      // Yêu cầu đảo bút toán
    public const string ReversalDecided        = "ReversalDecided";        // Phê duyệt / Từ chối đảo bút toán
    public const string ReconciliationResolved = "ReconciliationResolved"; // Xử lý lệch sổ
    public const string UserManagement         = "UserManagement";         // Tạo/khoá/đổi vai trò user
}


public static class OtpPurpose
{
    public const string Login = "Login";
}

// ── Phase 3: Bank-Grade Accounting & Compliance ──────────────────────────────

public static class GLAccountType
{
    public const string Asset = "Asset";
    public const string Liability = "Liability";
    public const string Equity = "Equity";
    public const string Income = "Income";
    public const string Expense = "Expense";
}

public static class LoanClassification
{
    public const int Group1_Standard = 1;         // Nợ đủ tiêu chuẩn
    public const int Group2_SpecialMention = 2;   // Nợ cần chú ý
    public const int Group3_Substandard = 3;      // Nợ dưới tiêu chuẩn
    public const int Group4_Doubtful = 4;         // Nợ nghi ngờ
    public const int Group5_Loss = 5;             // Nợ có khả năng mất vốn
}

public static class QualitativeClassificationReason
{
    public const string Bankruptcy = "Bankruptcy";              // Khách hàng phá sản
    public const string Litigation = "Litigation";              // Khách hàng đang bị khởi kiện
    public const string Fraud = "Fraud";                        // Có dấu hiệu lừa đảo/gian lận
    public const string Restructured = "Restructured";          // Khoản nợ cơ cấu lại
    public const string CrossContagion = "CrossContagion";      // Bị lây nhiễm nhóm nợ từ khoản vay khác
}

public static class CollateralHaircutRate
{
    public const decimal RealEstate = 0.50m;      // BĐS: 50%
    public const decimal Vehicle = 0.40m;         // Xe ô tô: 40%
    public const decimal SavingsBook = 0.95m;     // Sổ tiết kiệm: 95%
    public const decimal Other = 0.30m;           // Tài sản khác: 30%
}

