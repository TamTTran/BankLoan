using LoanFlow.Domain.Entities;
using LoanFlow.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(LoanFlowDbContext db)
    {
        if (await db.Branches.AnyAsync()) return; // Already seeded

        // Branches
        var hq = new Branch { Code = "HQ", Name = "Hội sở chính", Address = "123 Nguyễn Huệ, Q1, TP.HCM" };
        var hn = new Branch { Code = "HN01", Name = "Chi nhánh Hà Nội", Address = "45 Hoàng Cầu, Đống Đa, Hà Nội" };
        db.Branches.AddRange(hq, hn);
        await db.SaveChangesAsync();

        // Users
        var adminHash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
        var officerHash = BCrypt.Net.BCrypt.HashPassword("Officer@123");
        var approverHash = BCrypt.Net.BCrypt.HashPassword("Approver@123");
        var collectorHash = BCrypt.Net.BCrypt.HashPassword("Collector@123");

        var admin = new User { Username = "admin", PasswordHash = adminHash, FullName = "Quản trị hệ thống", Email = "admin@loanflow.vn", Role = Roles.Admin, BranchId = hq.Id };
        var officer = new User { Username = "officer01", PasswordHash = officerHash, FullName = "Nguyễn Văn An", Email = "officer01@loanflow.vn", Role = Roles.LoanOfficer, BranchId = hq.Id };
        var approver = new User { Username = "approver01", PasswordHash = approverHash, FullName = "Trần Thị Bình", Email = "approver01@loanflow.vn", Role = Roles.Approver, BranchId = hq.Id };
        var collector = new User { Username = "collector01", PasswordHash = collectorHash, FullName = "Lê Minh Cường", Email = "collector01@loanflow.vn", Role = Roles.Collector, BranchId = hq.Id };
        db.Users.AddRange(admin, officer, approver, collector);
        await db.SaveChangesAsync();

        // Loan Products
        var products = new List<LoanProduct>
        {
            new() {
                Code = "PROD-TIEUDUNG", Name = "Vay tiêu dùng cá nhân",
                Description = "Sản phẩm vay tiêu dùng dành cho cá nhân có thu nhập ổn định",
                MinAmount = 5_000_000, MaxAmount = 100_000_000,
                MinTenorMonths = 6, MaxTenorMonths = 36,
                InterestRatePerMonth = 1.5m, ProcessingFeeRate = 1.0m,
                CreatedByUserId = admin.Id
            },
            new() {
                Code = "PROD-MUAXE", Name = "Vay mua xe máy",
                Description = "Sản phẩm hỗ trợ mua xe máy trả góp",
                MinAmount = 10_000_000, MaxAmount = 50_000_000,
                MinTenorMonths = 12, MaxTenorMonths = 36,
                InterestRatePerMonth = 1.2m, ProcessingFeeRate = 0.5m,
                CreatedByUserId = admin.Id
            },
            new() {
                Code = "PROD-DOANHNGHIEP", Name = "Vay SME ngắn hạn",
                Description = "Giải pháp tài chính cho doanh nghiệp vừa và nhỏ",
                MinAmount = 50_000_000, MaxAmount = 500_000_000,
                MinTenorMonths = 3, MaxTenorMonths = 24,
                InterestRatePerMonth = 1.0m, ProcessingFeeRate = 0.8m,
                CreatedByUserId = admin.Id
            }
        };
        db.LoanProducts.AddRange(products);
        await db.SaveChangesAsync();

        Console.WriteLine("[Seeder] Done: Branches, Users, Loan Products seeded.");
    }
}
