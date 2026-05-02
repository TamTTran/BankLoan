using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface ICreditCheckService
{
    Task<Result<CreditCheckDto>> PerformCreditCheckAsync(int customerId, int? applicationId, int checkedByUserId);
    Task<IEnumerable<CreditCheckDto>> GetHistoryAsync(int customerId);
}
