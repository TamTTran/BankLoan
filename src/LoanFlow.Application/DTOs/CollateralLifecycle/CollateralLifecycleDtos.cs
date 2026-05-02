using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;

namespace LoanFlow.Application.Services;

public class RevaluationDto
{
    public int Id { get; set; }
    public int CollateralId { get; set; }
    public decimal OldValue { get; set; }
    public decimal NewValue { get; set; }
    public string? RevaluedBy { get; set; }
    public string? Reason { get; set; }
    public DateTime RevaluationDate { get; set; }
}

public class RevalueRequest
{
    public decimal NewValue { get; set; }
    public string? RevaluedBy { get; set; }
    public string? Reason { get; set; }
}
