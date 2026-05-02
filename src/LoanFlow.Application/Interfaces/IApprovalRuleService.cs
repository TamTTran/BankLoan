using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface IApprovalRuleService
{
    Task<IEnumerable<ApprovalRuleDto>> GetAllAsync();
    Task<Result<ApprovalRuleDto>> CreateAsync(CreateApprovalRuleRequest request);
    Task<Result> DeleteAsync(int id);
    Task<IEnumerable<ApprovalRuleDto>> GetRequiredRulesForApplicationAsync(int applicationId);
}
