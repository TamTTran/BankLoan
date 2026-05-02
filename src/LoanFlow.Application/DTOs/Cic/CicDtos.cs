using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;

namespace LoanFlow.Application.Services;

public class CicInquiryResultDto
{
    public int? Score { get; set; }
    public string? Grade { get; set; }
    public decimal? TotalBorrowing { get; set; }
    public int? MaxDpd { get; set; }
    public string? RawJson { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CicInquiryDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public DateTime InquiryDate { get; set; }
    public int? CicScore { get; set; }
    public string? CicGrade { get; set; }
    public decimal? TotalBorrowingAtCic { get; set; }
    public int? MaxDpdAtCic { get; set; }
    public string Status { get; set; } = "";
    public string Provider { get; set; } = "";
}
