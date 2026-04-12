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
    public int? CreditScore { get; set; }
    public string? CreditStatus { get; set; }
    public int AssignedToUserId { get; set; }
    public string? AssignedToFullName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CustomerListItemDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = default!;
    public string MaskedPhone { get; set; } = default!;
    public string MaskedIdNumber { get; set; } = default!;
    public string Status { get; set; } = default!;
    public int? CreditScore { get; set; }
    public string? CreditStatus { get; set; }
    public string? AssignedToFullName { get; set; }
    public DateTime CreatedAt { get; set; }
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

// ── Customer Assignment ─────────────────────────────────────────────────────
public class AssignCustomerRequest
{
    public int AssignedToUserId { get; set; }
}

// ── Customer Care Notes ─────────────────────────────────────────────────────
public class CreateCareNoteRequest
{
    public string InteractionType { get; set; } = default!;  // Call | Email | Zalo | SMS | Meeting | Other
    public DateTime InteractionDate { get; set; }
    public string Note { get; set; } = default!;
    public DateTime? NextFollowUpDate { get; set; }
}

public class CustomerCareNoteDto
{
    public int Id { get; set; }
    public string InteractionType { get; set; } = default!;
    public DateTime InteractionDate { get; set; }
    public string Note { get; set; } = default!;
    public DateTime? NextFollowUpDate { get; set; }
    public string CreatedByFullName { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}

// ── Lead Scoring ────────────────────────────────────────────────────────────
public class CustomerLeadDto
{
    public int CustomerId { get; set; }
    public string FullName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public int LeadScore { get; set; }
    public string LeadPriority { get; set; } = default!;    // Hot | Warm | Cold | Dormant
    public List<string> Signals { get; set; } = [];          // mô tả tín hiệu đã phát hiện
    public string? AssignedToFullName { get; set; }
    public DateTime? LastInteractionDate { get; set; }
}
