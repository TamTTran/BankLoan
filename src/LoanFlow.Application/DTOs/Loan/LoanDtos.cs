namespace LoanFlow.Application.DTOs.Loan;

public class LoanDto
{
    public int Id { get; set; }
    public string LoanAccountNo { get; set; } = default!;
    public int ApplicationId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = default!;
    public string ProductName { get; set; } = default!;
    public decimal ApprovedAmount { get; set; }
    public int TenorMonths { get; set; }
    public decimal MonthlyInterestRate { get; set; }
    public decimal ProcessingFee { get; set; }
    public DateOnly? DisbursementDate { get; set; }
    public DateOnly? MaturityDate { get; set; }
    public string Status { get; set; } = default!;
    public decimal OutstandingAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ScheduleItemDto> Schedule { get; set; } = [];
    public List<PaymentDto> Payments { get; set; } = [];
}

public class ScheduleItemDto
{
    public int InstallmentNo { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal PrincipalDue { get; set; }
    public decimal InterestDue { get; set; }
    public decimal TotalDue { get; set; }
    public decimal PrincipalPaid { get; set; }
    public decimal InterestPaid { get; set; }
    public decimal RemainingDueAmount { get; set; }
    public string Status { get; set; } = default!;
    public int Dpd { get; set; }        // computed at query time
}

public class PaymentDto
{
    public int Id { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = default!;
    public string? Remarks { get; set; }
    public string CreatedBy { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}

// POST /applications/{id}/create-loan (§17.8)
public class CreateLoanRequest
{
    // No body needed — loan is created from approved application
}

// POST /loans/{id}/disburse (§17.8)
public class DisburseRequest
{
    public DateOnly DisbursementDate { get; set; }
    public decimal DisbursedAmount { get; set; }
}

// POST /loans/{id}/payments (§17.9)
public class RecordPaymentRequest
{
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = default!;
    public string? Remarks { get; set; }
}

// POST /loans/{id}/collection-notes (§17.10)
public class AddCollectionNoteRequest
{
    public DateOnly NoteDate { get; set; }
    public string NoteText { get; set; } = default!;
}

public class CollectionNoteDto
{
    public int Id { get; set; }
    public DateOnly NoteDate { get; set; }
    public string NoteText { get; set; } = default!;
    public string CreatedBy { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}

public class OverdueItemDto
{
    public int LoanId { get; set; }
    public string LoanAccountNo { get; set; } = default!;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = default!;
    public string CustomerPhone { get; set; } = default!;
    public decimal OutstandingAmount { get; set; }
    public decimal OverdueDueAmount { get; set; }
    public int Dpd { get; set; }
    public string Bucket { get; set; } = default!;
    public string? LastCollectionNote { get; set; }
}

public class LoanFilterRequest
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int? CustomerId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
