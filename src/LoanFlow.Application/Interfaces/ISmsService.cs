namespace LoanFlow.Application.Services;

public interface ISmsService
{
    Task SendAsync(string phone, string message);
}
