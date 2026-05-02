using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Loan;

namespace LoanFlow.Application.Services;

public interface ILoanDisbursementService
{
    Task<Result<LoanDto>> CreateLoanAsync(int applicationId, int userId);
    Task<Result<LoanDto>> DisburseAsync(int loanId, DisburseRequest request, int userId);
}
