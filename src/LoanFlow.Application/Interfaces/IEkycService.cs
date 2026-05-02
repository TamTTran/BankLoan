using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface IEkycProvider
{
    string ProviderName { get; }
    Task<EkycResultDto> VerifyAsync(string idNumber, string idType, string fullName);
}

public interface IEkycService
{
    Task<Result<EkycVerificationDto>> VerifyCustomerAsync(int customerId, string idType, int requestedByUserId);
    Task<List<EkycVerificationDto>> GetHistoryAsync(int customerId);
}
