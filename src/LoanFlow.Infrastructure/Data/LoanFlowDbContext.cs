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

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LoanFlowDbContext).Assembly);
    }
}
