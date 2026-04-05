using LoanFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoanFlow.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Username).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.Username).IsUnique();
        builder.Property(x => x.PasswordHash).IsRequired();
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Email).HasMaxLength(150);
        builder.Property(x => x.Role).IsRequired().HasMaxLength(30);
        builder.HasOne(x => x.Branch).WithMany(b => b.Users).HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Address).HasMaxLength(300);
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CustomerCode).IsRequired().HasMaxLength(30);
        builder.HasIndex(x => x.CustomerCode).IsUnique();
        builder.Property(x => x.NationalId).IsRequired().HasMaxLength(20);
        builder.HasIndex(x => x.NationalId).IsUnique();
        builder.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(15);
        builder.HasIndex(x => x.PhoneNumber).IsUnique();
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Email).HasMaxLength(150);
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.Occupation).HasMaxLength(100);
        builder.Property(x => x.MonthlyIncome).HasMaxLength(50);
        builder.HasOne(x => x.Branch).WithMany(b => b.Customers).HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class LoanProductConfiguration : IEntityTypeConfiguration<LoanProduct>
{
    public void Configure(EntityTypeBuilder<LoanProduct> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(30);
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.MinAmount).HasPrecision(18, 2);
        builder.Property(x => x.MaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.InterestRatePerMonth).HasPrecision(8, 4);
        builder.Property(x => x.ProcessingFeeRate).HasPrecision(8, 4);
        builder.Property(x => x.RepaymentFrequency).HasMaxLength(20).HasDefaultValue("Monthly");
    }
}

public class LoanApplicationConfiguration : IEntityTypeConfiguration<LoanApplication>
{
    public void Configure(EntityTypeBuilder<LoanApplication> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ApplicationNumber).IsRequired().HasMaxLength(30);
        builder.HasIndex(x => x.ApplicationNumber).IsUnique();
        builder.Property(x => x.RequestedAmount).HasPrecision(18, 2);
        builder.Property(x => x.EstimatedInstallment).HasPrecision(18, 2);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(30).HasDefaultValue("Draft");
        builder.Property(x => x.Purpose).HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasOne(x => x.Customer).WithMany(c => c.LoanApplications).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LoanProduct).WithMany(p => p.LoanApplications).HasForeignKey(x => x.LoanProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Branch).WithMany(b => b.LoanApplications).HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LoanNumber).IsRequired().HasMaxLength(30);
        builder.HasIndex(x => x.LoanNumber).IsUnique();
        builder.Property(x => x.PrincipalAmount).HasPrecision(18, 2);
        builder.Property(x => x.InterestRatePerMonth).HasPrecision(8, 4);
        builder.Property(x => x.MonthlyInstallment).HasPrecision(18, 2);
        builder.Property(x => x.OutstandingBalance).HasPrecision(18, 2);
        builder.Property(x => x.TotalPaid).HasPrecision(18, 2);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Active");
        builder.HasOne(x => x.LoanApplication).WithOne(a => a.Loan).HasForeignKey<Loan>(x => x.LoanApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LoanProduct).WithMany().HasForeignKey(x => x.LoanProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RepaymentScheduleConfiguration : IEntityTypeConfiguration<RepaymentSchedule>
{
    public void Configure(EntityTypeBuilder<RepaymentSchedule> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PrincipalAmount).HasPrecision(18, 2);
        builder.Property(x => x.InterestAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalDue).HasPrecision(18, 2);
        builder.Property(x => x.PaidAmount).HasPrecision(18, 2);
        builder.Property(x => x.RemainingBalance).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Pending");
        builder.HasOne(x => x.Loan).WithMany(l => l.RepaymentSchedules).HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.PrincipalPaid).HasPrecision(18, 2);
        builder.Property(x => x.InterestPaid).HasPrecision(18, 2);
        builder.Property(x => x.FeePaid).HasPrecision(18, 2);
        builder.Property(x => x.PenaltyPaid).HasPrecision(18, 2);
        builder.Property(x => x.PaymentMethod).HasMaxLength(30);
        builder.Property(x => x.ReferenceNumber).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasOne(x => x.Loan).WithMany(l => l.Payments).HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CollectedBy).WithMany().HasForeignKey(x => x.CollectedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(50);
        builder.Property(x => x.EntityType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.EntityId).HasMaxLength(50);
        builder.Property(x => x.IpAddress).HasMaxLength(50);
        builder.HasOne(x => x.User).WithMany(u => u.AuditLogs).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}
