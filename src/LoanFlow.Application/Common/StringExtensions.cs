namespace LoanFlow.Application.Common;

public static class StringExtensions
{
    /// <summary>"0987654321" → "098****321"</summary>
    public static string MaskPhone(this string phone)
    {
        if (string.IsNullOrEmpty(phone) || phone.Length < 7) return "***";
        return phone[..3] + new string('*', phone.Length - 6) + phone[^3..];
    }

    /// <summary>"012345678901" → "012*****901"</summary>
    public static string MaskIdNumber(this string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length < 6) return "***";
        return id[..3] + new string('*', id.Length - 6) + id[^3..];
    }
}
