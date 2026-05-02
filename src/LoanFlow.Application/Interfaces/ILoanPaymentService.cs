using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Loan;

namespace LoanFlow.Application.Services;

public interface ILoanPaymentService
{
    Task<Result> RecordPaymentAsync(int loanId, RecordPaymentRequest request, int userId);
    Task<Result> ApplySuspenseAsync(int suspenseId, int userId);
    Task<Result<EarlySettlePreviewDto>> PreviewEarlySettleAsync(int loanId, DateOnly settlementDate);
    Task<Result> EarlySettleAsync(int loanId, EarlySettleRequest request, int userId);
}
