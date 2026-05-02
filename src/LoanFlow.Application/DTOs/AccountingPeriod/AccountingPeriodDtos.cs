using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;

namespace LoanFlow.Application.Services;

public class AccountingPeriodDto
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Status { get; set; } = "";
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosedByName { get; set; }
    public string? Notes { get; set; }
}
