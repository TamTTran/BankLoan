using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;

namespace LoanFlow.Application.Services;

public class CreditCheckDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int? ApplicationId { get; set; }
    public int TotalLoans { get; set; }
    public int ActiveLoans { get; set; }
    public int OverdueLoans { get; set; }
    public int ClosedLoans { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int MaxDpdLast12Months { get; set; }
    public int CreditScore { get; set; }
    public string CreditGrade { get; set; } = default!;
    public string Recommendation { get; set; } = default!;
    public DateTime CheckedAt { get; set; }
}
