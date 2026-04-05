namespace LoanFlow.Application.DTOs.Customer;

public class CustomerDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string IdNumber { get; set; } = default!;
    public string Address { get; set; } = default!;
    public DateOnly DateOfBirth { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// POST /customers
public class CreateCustomerRequest
{
    public string FullName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string IdNumber { get; set; } = default!;
    public string Address { get; set; } = default!;
    public DateOnly DateOfBirth { get; set; }
}

// PATCH /customers/{id}
public class UpdateCustomerRequest
{
    public string FullName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string Address { get; set; } = default!;
    public DateOnly DateOfBirth { get; set; }
}

public class CustomerFilterRequest
{
    public string? Search { get; set; }  // name | phone | idNumber
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
