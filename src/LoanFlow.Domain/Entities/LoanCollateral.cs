namespace LoanFlow.Domain.Entities;

/// <summary>
/// Junction table: many-to-many between Loan and Collateral.
/// A loan may have multiple collaterals; a collateral can secure multiple loans.
/// AllocatedValue tracks how much of the collateral's value is assigned to this loan.
/// LTV validation: sum(AllocatedValue) × LTV_ratio >= loan amount.
/// </summary>
public class LoanCollateral
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public int CollateralId { get; set; }
    public decimal AllocatedValue { get; set; }       // value portion allocated to this loan
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Loan Loan { get; set; } = default!;
    public Collateral Collateral { get; set; } = default!;
}
