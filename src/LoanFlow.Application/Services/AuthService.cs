using LoanFlow.Application.Common;
using LoanFlow.Application.DTOs.Auth;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace LoanFlow.Application.Services;

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request);
}

public class AuthService : IAuthService
{
    private readonly LoanFlowDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(LoanFlowDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users
            .Include(u => u.Branch)
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Result.Failure<LoginResponse>("Sai tên đăng nhập hoặc mật khẩu.");

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var token = GenerateJwtToken(user);
        var expiresAt = DateTime.UtcNow.AddHours(8);

        // Audit log
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id,
            Action = "LOGIN",
            EntityType = "User",
            EntityId = user.Id.ToString(),
            NewValues = $"{{\"username\":\"{user.Username}\",\"role\":\"{user.Role}\"}}"
        });
        await _db.SaveChangesAsync();

        return Result.Success(new LoginResponse(
            token, user.Username, user.FullName, user.Role,
            user.Id, user.BranchId, expiresAt
        ));
    }

    private string GenerateJwtToken(User user)
    {
        var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("fullName", user.FullName),
            new Claim("branchId", user.BranchId?.ToString() ?? ""),
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
