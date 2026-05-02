namespace LoanFlow.Application.Services;

public class LoanTopUpDto
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public string LoanAccountNo { get; set; } = "";
    public decimal TopUpAmount { get; set; }
    public decimal PreviousOutstanding { get; set; }
    public decimal NewOutstanding { get; set; }
    public string Status { get; set; } = "";
    public DateTime RequestedAt { get; set; }
}

public class TopUpRequest
{
    public decimal TopUpAmount { get; set; }
    public int NewTenorMonths { get; set; }
    public string? Remarks { get; set; }
}
