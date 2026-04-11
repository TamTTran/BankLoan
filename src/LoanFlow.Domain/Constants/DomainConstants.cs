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
    public const string CollateralCreated = "COLLATERAL_CREATED";
    public const string CollateralUpdated = "COLLATERAL_UPDATED";
    public const string CreditCheckPerformed = "CREDIT_CHECK_PERFORMED";
}
