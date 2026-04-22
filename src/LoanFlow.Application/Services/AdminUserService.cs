using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Admin;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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

public class AdminUserService : IAdminUserService
{
    private readonly LoanFlowDbContext _db;

    public AdminUserService(LoanFlowDbContext db) => _db = db;

    public async Task<IEnumerable<AdminUserDto>> GetUsersAsync()
    {
        var users = await _db.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
        return users.Select(u => new AdminUserDto
        {
            Id = u.Id, Username = u.Username, FullName = u.FullName,
            Role = u.Role, Status = u.Status, LockoutEnd = u.LockoutEnd,
            FailedLoginAttempts = u.FailedLoginAttempts, CreatedAt = u.CreatedAt
        });
    }

    public async Task<Result<int>> CreateUserAsync(CreateUserRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Username == request.Username))
            return Result.Failure<int>("Username already exists.");

        if (!System.Text.RegularExpressions.Regex.IsMatch(request.Password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{12,}$"))
            return Result.Failure<int>("Password must be at least 12 characters long and contain at least one uppercase letter, one lowercase letter, one number, and one special character.");

        var user = new User
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = request.FullName,
            Role = request.Role,
            Status = "Active"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Result.Success(user.Id);
    }

    public async Task<Result> ChangeUserStatusAsync(int userId, string status)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return Result.Failure("User not found.");
        
        user.Status = status; // Active or Inactive
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> ChangeUserRoleAsync(int userId, string role)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return Result.Failure("User not found.");
        
        user.Role = role;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> LockAccountAsync(int userId, int lockMinutes)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return Result.Failure("User not found.");
        
        user.LockoutEnd = DateTime.UtcNow.AddMinutes(lockMinutes);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> UnlockAccountAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return Result.Failure("User not found.");
        
        user.LockoutEnd = null;
        user.FailedLoginAttempts = 0;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Success();
    }
}
