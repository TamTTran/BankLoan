using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;

namespace LoanFlow.Application.Services;

public class ReconciliationReportDto
{
    public DateOnly BusinessDate { get; set; }
    public decimal TotalPaymentsAmount { get; set; }
    public decimal TotalPaymentTransactionsAmount { get; set; }
    public decimal TotalPaymentAllocationsAmount { get; set; }
    public decimal TotalDisbursementsAmount { get; set; }
    public decimal TotalDisbursementTransactionsAmount { get; set; }
    public decimal TotalReversalsAmount { get; set; }
    public decimal TotalReversalTransactionsAmount { get; set; }
    public decimal TotalLateFeesAmount { get; set; }
    public decimal TotalLateFeeTransactionsAmount { get; set; }
    public decimal TotalSuspenseAllocationsAmount { get; set; }
    public decimal TotalSuspenseTransactionsAmount { get; set; }
    public decimal TotalOutstandingComputedAmount { get; set; }
    public decimal TotalOutstandingStoredAmount { get; set; }
    public decimal TotalPendingLateFeesAmount { get; set; }
    public decimal TotalPendingSuspenseAmount { get; set; }
    public int ExceptionsFound { get; set; }
    public List<ReconciliationExceptionDto> Exceptions { get; set; } = [];
}

public class ReconciliationExceptionDto
{
    public int Id { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string ExceptionType { get; set; } = "";
    public int? LoanId { get; set; }
    public string? LoanAccountNo { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Difference { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "";
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
}

public class ResolveExceptionDto
{
    public string Resolution { get; set; } = "";
}
