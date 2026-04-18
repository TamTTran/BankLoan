namespace LoanFlow.Application.Common;

public class ConcurrencyConflictException : InvalidOperationException
{
    public ConcurrencyConflictException(string message) : base(message)
    {
    }
}
