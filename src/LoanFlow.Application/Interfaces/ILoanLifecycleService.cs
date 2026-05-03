using LoanFlow.Application.Common;
using LoanFlow.Domain.Entities;

namespace LoanFlow.Application.Services;

public interface ILoanLifecycleService
{
    Task<Result<LoanRestructure>> RestructureAsync(int loanId, RestructureRequest request, int userId);
    Task<Result<LoanReschedule>> RescheduleAsync(int loanId, RescheduleRequest request, int userId);
    Task<Result<LoanWriteOff>> WriteOffAsync(int loanId, WriteOffRequest request, int userId);
    Task<Result<LoanRecovery>> RecordRecoveryAsync(int loanId, RecordRecoveryRequest request, int userId);
    Task<Result<CustomerExposureDto>> GetCustomerExposureAsync(int customerId);
}
