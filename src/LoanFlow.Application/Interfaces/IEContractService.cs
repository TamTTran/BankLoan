using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface IEContractService
{
    Task<Result<EContractDto>> SignContractAsync(int loanId, string ipAddress);
    Task<Result<EContractDto>> GetByLoanIdAsync(int loanId);
}

public interface ICertificateAuthorityProvider
{
    Task<string> SignHashAsync(string hashStr);
}
