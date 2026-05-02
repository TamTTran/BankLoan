using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface ILateFeeService
{
    Task<Result> CalculateLateFeesAsync(int loanId, DateOnly calculationDate);
    Task<Result> CalculateLateFeesAsync(Loan loan, DateOnly calculationDate);
    Task<Result> WaiveLateFeeAsync(int lateFeeId, int userId);
}
