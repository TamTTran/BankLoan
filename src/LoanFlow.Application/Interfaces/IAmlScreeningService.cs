namespace LoanFlow.Application.Services;

public interface ISanctionListProvider
{
    Task<(bool IsHit, string? Details)> CheckNameAsync(string fullName, string? idNumber);
}

public interface IAmlScreeningService
{
    Task<AmlRiskAssessment> ScreenCustomerAsync(int customerId, int currentUserId);
}
