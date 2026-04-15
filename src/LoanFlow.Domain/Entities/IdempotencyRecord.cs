namespace LoanFlow.Domain.Entities;

public class IdempotencyRecord
{
    public string IdempotencyKey { get; set; } = null!;
    public string? ResponseBody { get; set; }
    public int StatusCode { get; set; }
    public DateTime CreatedAt { get; set; }
}
