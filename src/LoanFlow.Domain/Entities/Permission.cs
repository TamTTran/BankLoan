namespace LoanFlow.Domain.Entities;

public class Permission
{
    public string Code { get; set; } = default!; // e.g., "LOAN_CREATE", "CUSTOMER_VIEW"
    public string Name { get; set; } = default!;
    public string Module { get; set; } = default!; // e.g., "Loans", "Customers"
}

public class RolePermission
{
    public string RoleCode { get; set; } = default!; // e.g., "Admin", "LoanOfficer"
    public string PermissionCode { get; set; } = default!;

    public Permission Permission { get; set; } = default!;
}
