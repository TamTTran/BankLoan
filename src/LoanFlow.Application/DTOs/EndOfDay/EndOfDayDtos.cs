using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;

namespace LoanFlow.Application.Services;

public class EodReportDto
{
    public DateOnly BusinessDate { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration => CompletedAt - StartedAt;

    // Step results
    public OverdueJobResultDto? OverdueResult { get; set; }
    public ReconciliationReportDto? ReconciliationResult { get; set; }

    // Portfolio summary
    public int TotalActiveLoans { get; set; }
    public int TotalOverdueLoans { get; set; }
    public int TotalClosedLoans { get; set; }
    public decimal TotalDisbursedToday { get; set; }
    public decimal TotalPaymentsToday { get; set; }
    public int Errors { get; set; }
    public int BrokenPromises { get; set; }
    public List<string> ErrorMessages { get; set; } = [];
}
