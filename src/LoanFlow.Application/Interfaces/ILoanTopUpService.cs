using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface ILoanTopUpService
{
    Task<Result<LoanTopUpDto>> RequestTopUpAsync(int loanId, TopUpRequest req, int userId);
    Task<Result> ApproveTopUpAsync(int topUpId, int userId);
    Task<Result> RejectTopUpAsync(int topUpId, int userId);
    Task<List<LoanTopUpDto>> GetTopUpsAsync(int? loanId);
}
