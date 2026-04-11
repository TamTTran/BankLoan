namespace LoanFlow.Application.DTOs.Collateral;

public class CollateralDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CollateralType { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string? RegistrationNo { get; set; }
    public decimal AppraisedValue { get; set; }
    public string? AppraisedBy { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}

public class CreateCollateralRequest
{
    public int CustomerId { get; set; }
    public string CollateralType { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string? RegistrationNo { get; set; }
    public decimal AppraisedValue { get; set; }
    public string? AppraisedBy { get; set; }
    public string? Address { get; set; }
}

public class UpdateCollateralRequest
{
    public string CollateralType { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string? RegistrationNo { get; set; }
    public decimal AppraisedValue { get; set; }
    public string? AppraisedBy { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = default!;
}

public class LoanCollateralDto
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public int CollateralId { get; set; }
    public decimal AllocatedValue { get; set; }
    public CollateralDto Collateral { get; set; } = default!;
}

public class AllocateCollateralRequest
{
    public int CollateralId { get; set; }
    public decimal AllocatedValue { get; set; }
}
