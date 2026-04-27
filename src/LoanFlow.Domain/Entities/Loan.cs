using LoanFlow.Domain.Constants;

namespace LoanFlow.Domain.Entities;

public class Loan
{
    public int Id { get; set; }
    public string LoanAccountNo { get; set; } = default!;   // unique — e.g. LN-2026-0001
    public int ApplicationId { get; set; }
    public int CustomerId { get; set; }
    public int ProductId { get; set; }
    public decimal ApprovedAmount { get; set; }
    public int TenorMonths { get; set; }
    public decimal MonthlyInterestRate { get; set; }        // snapshot from product
    public decimal ProcessingFee { get; set; }              // snapshot from product
    public DateOnly? DisbursementDate { get; set; }
    public DateOnly? MaturityDate { get; set; }
    public string Status { get; set; } = "PendingDisbursement"; // PendingDisbursement | Active | Overdue | Closed
    public decimal OutstandingAmount { get; set; }

    // Phase 4: Virtual Account for Payments
    public string? VirtualAccountNo { get; set; }

    // ── Phase 3: NPL & Classification ────────────────────────────────────────
    public int CurrentClassification { get; set; } = LoanClassification.Group1_Standard;
    public decimal ProvisionAmount { get; set; } = 0;

    // Phân loại định tính (Qualitative)
    public int? QualitativeClassification { get; set; }
    public string? QualitativeReason { get; set; }

    // ── NHNN Reporting Fields ────────────────────────────────────────
    public string CurrencyCode { get; set; } = "VND";
    public string? PurposeCode { get; set; }
    public string? IndustryCode { get; set; }


    // ── Bank-grade snapshot fields (frozen at loan creation from product) ────
    public string InterestMethod { get; set; } = "Flat";    // snapshot: Flat | ReducingBalance
    public int GracePeriodMonths { get; set; } = 0;         // snapshot: grace period from product
    public decimal? PromotionalRate { get; set; }           // snapshot: promo rate from product
    public int PromotionalMonths { get; set; } = 0;         // snapshot: promo months from product
    public decimal EarlySettlementFeeRate { get; set; } = 0;// snapshot: early settlement penalty rate
    public decimal LateFeeRate { get; set; } = 0;           // snapshot: late fee rate
    public int LateFeeGraceDays { get; set; } = 0;          // snapshot: grace days before late fee
    public string ProductType { get; set; } = "PersonalUnsecured"; // snapshot: product type
    public int? BranchId { get; set; }                            // chi nhánh sở hữu khoản vay

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RowVersion { get; set; }

    public LoanApplication Application { get; set; } = default!;
    public Customer Customer { get; set; } = default!;
    public LoanProduct Product { get; set; } = default!;
    public LoanDisbursement? Disbursement { get; set; }
    public ICollection<RepaymentSchedule> RepaymentSchedules { get; set; } = new List<RepaymentSchedule>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<LoanTransaction> Transactions { get; set; } = new List<LoanTransaction>();
    public ICollection<CollectionNote> CollectionNotes { get; set; } = new List<CollectionNote>();
    public ICollection<LateFee> LateFees { get; set; } = new List<LateFee>();
    public ICollection<LoanCollateral> LoanCollaterals { get; set; } = new List<LoanCollateral>();
}
