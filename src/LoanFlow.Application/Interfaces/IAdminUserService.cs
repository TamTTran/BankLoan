using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Admin;

namespace LoanFlow.Application.Services;

public interface IAdminUserService
{
    Task<IEnumerable<AdminUserDto>> GetUsersAsync();
    Task<Result<int>> CreateUserAsync(CreateUserRequest request);
    Task<Result> ChangeUserStatusAsync(int userId, string status);
    Task<Result> ChangeUserRoleAsync(int userId, string role);
    Task<Result> LockAccountAsync(int userId, int lockMinutes);
    Task<Result> UnlockAccountAsync(int userId);
}
