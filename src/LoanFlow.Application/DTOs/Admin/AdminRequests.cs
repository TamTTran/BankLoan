namespace LoanFlow.Application.DTOs.Admin;

public class AdminUserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? LockoutEnd { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class ChangeRoleRequest
{
    public string Role { get; set; } = string.Empty;
}
