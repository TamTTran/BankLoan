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
}
