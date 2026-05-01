using System.ComponentModel.DataAnnotations;

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
    public string? VirtualAccountNo { get; set; }
    public string? DisbursementMethod { get; set; }
    public string? DisbursementStatus { get; set; }
    public string? NapasReferenceId { get; set; }
    public string? NapasTransactionId { get; set; }
    public DateTime? GatewayConfirmedAt { get; set; }
    public string? GatewayErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ScheduleItemDto> Schedule { get; set; } = [];
    public List<PaymentDto> Payments { get; set; } = [];
    public List<LateFeeDto> LateFees { get; set; } = [];
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
    public decimal UnpaidLateFeeAmount { get; set; } // computed at query time
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
    [Required]
    public DateOnly DisbursementDate { get; set; }
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public decimal DisbursedAmount { get; set; }
}

// POST /loans/{id}/payments (§17.9)
public class RecordPaymentRequest
{
    [Required]
    public DateOnly PaymentDate { get; set; }
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }
    [Required]
    [StringLength(50)]
    public string PaymentMethod { get; set; } = default!;
    [StringLength(500)]
    public string? Remarks { get; set; }
    [StringLength(100)]
    public string? ExternalReferenceId { get; set; }
}

// POST /loans/{id}/collection-notes (§17.10)
public class AddCollectionNoteRequest
{
    [Required]
    public DateOnly NoteDate { get; set; }
    [Required]
    [StringLength(1000, MinimumLength = 1)]
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
    [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100.")]
    public int PageSize { get; set; } = 20;
}

// ── Bank-grade Phase 2 DTOs ────────────────────────────────────────────────

public class EarlySettleRequest
{
    [Required]
    public DateOnly SettlementDate { get; set; }
    public bool WaiveFee { get; set; } = false;
}

public class EarlySettlePreviewDto
{
    public DateOnly SettlementDate { get; set; }
    public decimal RemainingPrincipal { get; set; }
    public decimal ProRataInterest { get; set; }
    public decimal EarlySettlementFee { get; set; }
    public decimal UnpaidLateFees { get; set; }
    public decimal TotalSettlementAmount { get; set; }
}

public class LateFeeDto
{
    public int Id { get; set; }
    public int ScheduleId { get; set; }
    public decimal OverdueAmount { get; set; }
    public int DaysOverdue { get; set; }
    public decimal FeeRate { get; set; }
    public decimal FeeAmount { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CalculatedAt { get; set; }
}
