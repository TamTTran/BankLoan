using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Loan;

namespace LoanFlow.Application.Services;

public interface ILoanQueryService
{
    Task<PagedResult<LoanDto>> GetListAsync(LoanFilterRequest filter);
    Task<Result<LoanDto>>      GetByIdAsync(int id);
    Task<IEnumerable<ScheduleItemDto>> GetScheduleAsync(int loanId);
    Task<IEnumerable<PaymentDto>>      GetPaymentsAsync(int loanId);
    Task<IEnumerable<OverdueItemDto>>  GetOverdueListAsync();
}
