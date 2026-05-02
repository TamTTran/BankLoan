using LoanFlow.Application.Common;

namespace LoanFlow.Application.Services;

public interface ICicProvider
{
    string ProviderName { get; }
    Task<CicInquiryResultDto> InquireAsync(string idNumber, string fullName);
}

public interface ICicService
{
    Task<Result<CicInquiryDto>> InquireCicAsync(int customerId, int requestedByUserId);
    Task<List<CicInquiryDto>> GetHistoryAsync(int customerId);
}
