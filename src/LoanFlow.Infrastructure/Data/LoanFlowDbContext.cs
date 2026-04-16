using LoanFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Infrastructure.Data;

public class LoanFlowDbContext : DbContext
{
    public LoanFlowDbContext(DbContextOptions<LoanFlowDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<LoanProduct> LoanProducts => Set<LoanProduct>();
    public DbSet<LoanApplication> LoanApplications => Set<LoanApplication>();
    public DbSet<ApplicationDecision> ApplicationDecisions => Set<ApplicationDecision>();
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<LoanDisbursement> LoanDisbursements => Set<LoanDisbursement>();
    public DbSet<RepaymentSchedule> RepaymentSchedules => Set<RepaymentSchedule>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<LoanTransaction> LoanTransactions => Set<LoanTransaction>();
    public DbSet<CollectionNote> CollectionNotes => Set<CollectionNote>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // ── Bank-grade entities ─────────────────────────────────────────────────
    public DbSet<LateFee> LateFees => Set<LateFee>();
    public DbSet<Collateral> Collaterals => Set<Collateral>();
    public DbSet<LoanCollateral> LoanCollaterals => Set<LoanCollateral>();
    public DbSet<ApprovalRule> ApprovalRules => Set<ApprovalRule>();
    public DbSet<CreditCheck> CreditChecks => Set<CreditCheck>();
    public DbSet<CustomerCareNote> CustomerCareNotes => Set<CustomerCareNote>();
    public DbSet<OverdueDailySnapshot> OverdueDailySnapshots => Set<OverdueDailySnapshot>();

    // ── Wave 2: Reversal & Reconciliation ────────────────────────────────────
    public DbSet<TransactionReversal> TransactionReversals => Set<TransactionReversal>();
    public DbSet<ReconciliationException> ReconciliationExceptions => Set<ReconciliationException>();

    // ── Wave 3: Collections ───────────────────────────────────────────────────
    public DbSet<CollectionAction> CollectionActions => Set<CollectionAction>();
    public DbSet<CollectorAssignment> CollectorAssignments => Set<CollectorAssignment>();

    // ── Wave 4: Overpayment & Notifications ──────────────────────────────────
    public DbSet<SuspenseTransaction> SuspenseTransactions => Set<SuspenseTransaction>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<EmailOtp> EmailOtps => Set<EmailOtp>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<IdempotencyRecord>(b =>
        {
            b.HasKey(x => x.IdempotencyKey);
            b.Property(x => x.IdempotencyKey).HasMaxLength(128);
        });

        modelBuilder.Entity<TransactionReversal>(b =>
        {
            b.HasOne(r => r.OriginalTransaction)
                .WithMany()
                .HasForeignKey(r => r.OriginalTransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(r => r.ReversalTransaction)
                .WithMany()
                .HasForeignKey(r => r.ReversalTransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(r => r.RequestedBy)
                .WithMany()
                .HasForeignKey(r => r.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(r => r.ApprovedBy)
                .WithMany()
                .HasForeignKey(r => r.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CollectorAssignment>(b =>
        {
            b.HasOne(a => a.Collector)
                .WithMany()
                .HasForeignKey(a => a.CollectorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CollectionAction>(b =>
        {
            b.HasOne(a => a.Collector)
                .WithMany()
                .HasForeignKey(a => a.CollectorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SuspenseTransaction>(b =>
        {
            b.Property(s => s.Amount).HasPrecision(18, 2);
            b.HasOne(s => s.AppliedToSchedule)
                .WithMany()
                .HasForeignKey(s => s.AppliedToRepaymentScheduleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── H5: Performance indexes for 100K+ scale ─────────────────────────
        modelBuilder.Entity<Loan>(b =>
        {
            b.HasIndex(l => l.Status).HasDatabaseName("IX_Loans_Status");
            b.HasIndex(l => l.LoanAccountNo).IsUnique().HasDatabaseName("IX_Loans_LoanAccountNo");
        });

        modelBuilder.Entity<RepaymentSchedule>(b =>
        {
            b.HasIndex(s => new { s.DueDate, s.Status }).HasDatabaseName("IX_RepaymentSchedules_DueDate_Status");
            b.HasIndex(s => s.LoanId).HasDatabaseName("IX_RepaymentSchedules_LoanId");
        });

        modelBuilder.Entity<Notification>(b =>
        {
            b.HasIndex(n => new { n.UserId, n.IsRead }).HasDatabaseName("IX_Notifications_UserId_IsRead");
            b.HasIndex(n => new { n.Type, n.LoanId }).HasDatabaseName("IX_Notifications_Type_LoanId");
        });

        modelBuilder.Entity<Payment>(b =>
        {
            b.HasIndex(p => p.PaymentDate).HasDatabaseName("IX_Payments_PaymentDate");
            b.HasIndex(p => p.LoanId).HasDatabaseName("IX_Payments_LoanId");
        });

        modelBuilder.Entity<EmailOtp>(b =>
        {
            b.HasIndex(o => o.DeeplinkToken).HasDatabaseName("IX_EmailOtps_DeeplinkToken");
            b.HasIndex(o => new { o.UserId, o.CreatedAt }).HasDatabaseName("IX_EmailOtps_UserId_CreatedAt");
        });

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.HasIndex(a => new { a.EntityType, a.EntityId }).HasDatabaseName("IX_AuditLogs_EntityType_EntityId");
        });

        modelBuilder.Entity<CollectorAssignment>(b =>
        {
            b.HasIndex(a => new { a.CollectorUserId, a.Status }).HasDatabaseName("IX_CollectorAssignments_Collector_Status");
        });

        modelBuilder.Entity<Customer>(b =>
        {
            b.HasIndex(c => c.Phone).IsUnique().HasDatabaseName("IX_Customers_Phone");
            b.HasIndex(c => c.IdNumber).IsUnique().HasDatabaseName("IX_Customers_IdNumber");
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LoanFlowDbContext).Assembly);
    }
}
