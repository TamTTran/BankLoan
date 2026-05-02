using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Customer;

namespace LoanFlow.Application.Services;

public interface ICustomerService
{
    Task<PagedResult<CustomerListItemDto>> GetListAsync(CustomerFilterRequest filter);
    Task<Result<CustomerDto>> GetByIdAsync(int id);
    Task<Result<CustomerDto>> CreateAsync(CreateCustomerRequest request, int createdByUserId);
    Task<Result<CustomerDto>> UpdateAsync(int id, UpdateCustomerRequest request);
    Task<Result<CustomerDto>> CheckCreditStatusAsync(int id, int checkedByUserId);

    // ── Customer Intelligence ────────────────────────────────────────────────
    Task<Result<CustomerDto>> AssignAsync(int customerId, AssignCustomerRequest req, int actorUserId);
    Task<Result<CustomerCareNoteDto>> AddCareNoteAsync(int customerId, CreateCareNoteRequest req, int actorUserId);
    Task<IEnumerable<CustomerCareNoteDto>> GetCareNotesAsync(int customerId);
}
