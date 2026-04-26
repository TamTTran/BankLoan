namespace LoanFlow.Application.Common;

/// <summary>
/// Thrown when a domain/business rule is violated and the message is safe
/// to expose to the API consumer. Framework-level InvalidOperationException
/// should NOT be caught by the same handler, because those messages may
/// leak internal implementation details.
/// </summary>
public class DomainValidationException : Exception
{
    public DomainValidationException(string message) : base(message)
    {
    }

    public DomainValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
