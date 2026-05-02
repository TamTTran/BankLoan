using LoanFlow.Application.DTOs.Customer;

namespace LoanFlow.Application.Services;

public interface ICustomerLeadService
{
    /// <summary>
    /// Tính Lead Score cho tất cả khách hàng thuộc phạm vi nhân viên.
    /// Manager/Admin: toàn bộ. LoanOfficer: chỉ khách mình phụ trách.
    /// </summary>
    Task<List<CustomerLeadDto>> GetLeadBoardAsync(int? assignedToUserId = null);
}
