using LoanFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoanFlow.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Username).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.Username).IsUnique();
        builder.Property(x => x.PasswordHash).IsRequired().HasMaxLength(255);
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Role).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Active");
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Phone).IsRequired().HasMaxLength(20);
        builder.HasIndex(x => x.Phone).IsUnique();
        builder.Property(x => x.IdNumber).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.IdNumber).IsUnique();
        builder.Property(x => x.Address).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Active");
        builder.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class LoanProductConfiguration : IEntityTypeConfiguration<LoanProduct>
{
    public void Configure(EntityTypeBuilder<LoanProduct> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProductName).IsRequired().HasMaxLength(150);
        builder.HasIndex(x => x.ProductName).IsUnique();
        builder.Property(x => x.MinAmount).HasPrecision(18, 2);
        builder.Property(x => x.MaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.AllowedTenorsJson).IsRequired().HasMaxLength(200);
        builder.Property(x => x.MonthlyInterestRate).HasPrecision(10, 6);
        builder.Property(x => x.ProcessingFee).HasPrecision(18, 2);
        builder.Property(x => x.RepaymentFrequency).HasMaxLength(20).HasDefaultValue("Monthly");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Active");
    }
}

public class LoanApplicationConfiguration : IEntityTypeConfiguration<LoanApplication>
{
    public void Configure(EntityTypeBuilder<LoanApplication> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestedAmount).HasPrecision(18, 2);
        builder.Property(x => x.Purpose).HasMaxLength(255);
        builder.Property(x => x.Remarks).HasMaxLength(2000);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Draft");
        builder.HasOne(x => x.Customer).WithMany(c => c.LoanApplications)
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Product).WithMany(p => p.LoanApplications)
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedBy).WithMany()
            .HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ApplicationDecisionConfiguration : IEntityTypeConfiguration<ApplicationDecision>
{
    public void Configure(EntityTypeBuilder<ApplicationDecision> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Decision).IsRequired().HasMaxLength(20);
        builder.Property(x => x.DecisionReason).HasMaxLength(2000);
        builder.HasOne(x => x.Application).WithMany(a => a.Decisions)
            .HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DecidedBy).WithMany()
            .HasForeignKey(x => x.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LoanAccountNo).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.LoanAccountNo).IsUnique();
        builder.Property(x => x.ApprovedAmount).HasPrecision(18, 2);
        builder.Property(x => x.MonthlyInterestRate).HasPrecision(10, 6);
        builder.Property(x => x.ProcessingFee).HasPrecision(18, 2);
        builder.Property(x => x.OutstandingAmount).HasPrecision(18, 2);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(30).HasDefaultValue("PendingDisbursement");
        builder.HasOne(x => x.Application).WithOne(a => a.Loan)
            .HasForeignKey<Loan>(x => x.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Customer).WithMany()
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Product).WithMany()
            .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class LoanDisbursementConfiguration : IEntityTypeConfiguration<LoanDisbursement>
{
    public void Configure(EntityTypeBuilder<LoanDisbursement> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DisbursedAmount).HasPrecision(18, 2);
        builder.HasOne(x => x.Loan).WithOne(l => l.Disbursement)
            .HasForeignKey<LoanDisbursement>(x => x.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RecordedBy).WithMany()
            .HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RepaymentScheduleConfiguration : IEntityTypeConfiguration<RepaymentSchedule>
{
    public void Configure(EntityTypeBuilder<RepaymentSchedule> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PrincipalDue).HasPrecision(18, 2);
        builder.Property(x => x.InterestDue).HasPrecision(18, 2);
        builder.Property(x => x.PrincipalPaid).HasPrecision(18, 2);
        builder.Property(x => x.InterestPaid).HasPrecision(18, 2);
        builder.Property(x => x.RemainingDueAmount).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("Unpaid");
        builder.HasOne(x => x.Loan).WithMany(l => l.RepaymentSchedules)
            .HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.PaymentMethod).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Remarks).HasMaxLength(500);
        builder.HasOne(x => x.Loan).WithMany(l => l.Payments)
            .HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedBy).WithMany()
            .HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class LoanTransactionConfiguration : IEntityTypeConfiguration<LoanTransaction>
{
    public void Configure(EntityTypeBuilder<LoanTransaction> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TransactionType).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.ReferenceNo).HasMaxLength(50);
        builder.HasOne(x => x.Loan).WithMany(l => l.Transactions)
            .HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Payment).WithMany()
            .HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
    }
}

public class CollectionNoteConfiguration : IEntityTypeConfiguration<CollectionNote>
{
    public void Configure(EntityTypeBuilder<CollectionNote> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NoteText).IsRequired();
        builder.HasOne(x => x.Loan).WithMany(l => l.CollectionNotes)
            .HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedBy).WithMany()
            .HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActionCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.EntityType).IsRequired().HasMaxLength(50);
        builder.HasOne(x => x.ActorUser).WithMany(u => u.AuditLogs)
            .HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
