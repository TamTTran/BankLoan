using System;
using System.Linq;
using System.Threading.Tasks;
using LoanFlow.Application.Common;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;

namespace LoanFlow.Application.Services;

public class RestructureRequest
{
    public int NewTenorMonths { get; set; }
    public decimal NewMonthlyInterestRate { get; set; }
    public DateOnly RestructureStartDate { get; set; }
    public string ReasonCode { get; set; } = default!;  // "CustomerHardship" | "BusinessFailure"
    public string? Note { get; set; }
}

public class RescheduleRequest
{
    public int NewPaymentDayOfMonth { get; set; }       // 1 - 28
    public DateOnly EffectiveFrom { get; set; }
    public string ReasonCode { get; set; } = default!;
    public string? Note { get; set; }
}

public class WriteOffRequest
{
    public string ReasonCode { get; set; } = default!;  // "Uncollectible" | "BankruptCustomer" | "CourtOrder"
    public DateOnly WriteOffDate { get; set; }
    public string? Note { get; set; }
}

public class RecordRecoveryRequest
{
    public decimal RecoveredAmount { get; set; }
    public DateOnly RecoveryDate { get; set; }
    public string RecoveryMethod { get; set; } = default!; // "CashPayment" | "CollateralLiquidation" | "ThirdPartyCollection"
    public string? Note { get; set; }
}

public class CustomerExposureDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = default!;
    public int ActiveLoans { get; set; }
    public int RestructuredLoans { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal TotalApprovedAmount { get; set; }
    public bool ExceedsLimit { get; set; }
    public decimal MaxAllowedExposure { get; set; }
}
