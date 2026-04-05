namespace LoanFlow.Domain.Constants;

public static class Roles
{
    public const string Admin = "Admin";
    public const string LoanOfficer = "LoanOfficer";
    public const string Approver = "Approver";
    public const string Collector = "Collector";
    public static readonly string[] All = [Admin, LoanOfficer, Approver, Collector];
}

public static class ApplicationStatus
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string UnderReview = "UnderReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
}

public static class LoanStatus
{
    public const string Active = "Active";
    public const string Closed = "Closed";
    public const string Defaulted = "Defaulted";
    public const string WrittenOff = "WrittenOff";
}

public static class ScheduleStatus
{
    public const string Pending = "Pending";
    public const string PartiallyPaid = "PartiallyPaid";
    public const string Paid = "Paid";
    public const string Overdue = "Overdue";
}

public static class DpdBucket
{
    public const string Current = "Current";
    public const string Dpd1To7 = "DPD1_7";
    public const string Dpd8To30 = "DPD8_30";
    public const string Dpd31To90 = "DPD31_90";
    public const string Dpd90Plus = "DPD90Plus";

    public static string Calculate(int daysOverdue) => daysOverdue switch
    {
        0 => Current,
        >= 1 and <= 7 => Dpd1To7,
        >= 8 and <= 30 => Dpd8To30,
        >= 31 and <= 90 => Dpd31To90,
        _ => Dpd90Plus
    };
}
