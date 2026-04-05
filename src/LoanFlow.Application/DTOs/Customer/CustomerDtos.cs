namespace LoanFlow.Application.DTOs.Customer;

public class CustomerDto
{
    public int Id { get; set; }
    public string CustomerCode { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string NationalId { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    public string? Email { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? Occupation { get; set; }
    public string? MonthlyIncome { get; set; }
    public string? BranchName { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ActiveLoans { get; set; }
    public decimal TotalOutstanding { get; set; }
}

public class CreateCustomerRequest
{
    public string FullName { get; set; } = default!;
    public string NationalId { get; set; } = default!;
    public string PhoneNumber { get; set; } = default!;
    public string? Email { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? Occupation { get; set; }
    public string? MonthlyIncome { get; set; }
    public int? BranchId { get; set; }
}

public class UpdateCustomerRequest
{
    public string FullName { get; set; } = default!;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Occupation { get; set; }
    public string? MonthlyIncome { get; set; }
    public string PhoneNumber { get; set; } = default!;
}

public class CustomerFilterRequest
{
    public string? Search { get; set; } // name | nationalId | phone
    public int? BranchId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
