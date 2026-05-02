using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;

namespace LoanFlow.Application.Services;

public class ApprovalRuleDto
{
    public int Id { get; set; }
    public string? ProductType { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int RequiredLevel { get; set; }
    public string ApproverRole { get; set; } = default!;
    public int? MinCicScore { get; set; }
    public bool IsAutoApprove { get; set; }
}

public class CreateApprovalRuleRequest
{
    public string? ProductType { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int RequiredLevel { get; set; }
    public string ApproverRole { get; set; } = default!;
    public int? MinCicScore { get; set; }
    public bool IsAutoApprove { get; set; }
}
