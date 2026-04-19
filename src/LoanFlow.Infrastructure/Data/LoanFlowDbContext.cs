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

    // ── Banking Upgrade: Maker-Checker ───────────────────────────────────────
    public DbSet<PendingApprovalRequest> PendingApprovalRequests => Set<PendingApprovalRequest>();
    public DbSet<LoanAdjustment> LoanAdjustments => Set<LoanAdjustment>();

    // ── Banking Upgrade: Phase 2 ─────────────────────────────────────────
    public DbSet<LoanRestructure> LoanRestructures => Set<LoanRestructure>();
    public DbSet<LoanReschedule> LoanReschedules => Set<LoanReschedule>();
    public DbSet<LoanWriteOff> LoanWriteOffs => Set<LoanWriteOff>();
    public DbSet<LoanRecovery> LoanRecoveries => Set<LoanRecovery>();

    // ── Banking Upgrade: Phase 3 (Accounting) ────────────────────────────
    public DbSet<GLAccount> GLAccounts => Set<GLAccount>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();

    // ── Banking Upgrade: Phase 4 (Enterprise) ────────────────────────────
    public DbSet<ClassificationSnapshot> ClassificationSnapshots => Set<ClassificationSnapshot>();
    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    public DbSet<CicInquiry> CicInquiries => Set<CicInquiry>();
    public DbSet<EkycVerification> EkycVerifications => Set<EkycVerification>();
    public DbSet<Branch> Branches => Set<Branch>();

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
            b.Property(l => l.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<RepaymentSchedule>(b =>
        {
            b.HasIndex(s => new { s.DueDate, s.Status }).HasDatabaseName("IX_RepaymentSchedules_DueDate_Status");
            b.HasIndex(s => s.LoanId).HasDatabaseName("IX_RepaymentSchedules_LoanId");
            b.Property(s => s.RowVersion).IsRowVersion();
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

        modelBuilder.Entity<LateFee>(b =>
        {
            b.Property(f => f.RowVersion).IsRowVersion();
        });

        modelBuilder.Entity<TransactionReversal>(b =>
        {
            b.Property(r => r.RowVersion).IsRowVersion();
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

        modelBuilder.Entity<PendingApprovalRequest>(b =>
        {
            b.HasIndex(p => p.Status).HasDatabaseName("IX_PendingApprovalRequests_Status");
            b.Property(p => p.ActionType).HasMaxLength(50);
            b.Property(p => p.Status).HasMaxLength(20);
            
            b.HasOne(p => p.RequestedBy)
                .WithMany()
                .HasForeignKey(p => p.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(p => p.ApprovedBy)
                .WithMany()
                .HasForeignKey(p => p.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LoanAdjustment>(b =>
        {
            b.HasIndex(a => a.LoanId).HasDatabaseName("IX_LoanAdjustments_LoanId");
            b.Property(a => a.AdjustmentType).HasMaxLength(50);
            b.Property(a => a.ReasonCode).HasMaxLength(50);
            b.Property(a => a.Amount).HasPrecision(18, 2);

            b.HasOne(a => a.Loan)
                .WithMany()
                .HasForeignKey(a => a.LoanId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(a => a.CreatedBy)
                .WithMany()
                .HasForeignKey(a => a.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(a => a.ApprovedBy)
                .WithMany()
                .HasForeignKey(a => a.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Phase 2: Loan Lifecycle ───────────────────────────────────
        modelBuilder.Entity<LoanRestructure>(b =>
        {
            b.HasIndex(r => r.LoanId).HasDatabaseName("IX_LoanRestructures_LoanId");
            b.Property(r => r.ReasonCode).HasMaxLength(50);
            b.Property(r => r.OldMonthlyInterestRate).HasPrecision(10, 6);
            b.Property(r => r.NewMonthlyInterestRate).HasPrecision(10, 6);
            b.HasOne(r => r.Loan).WithMany().HasForeignKey(r => r.LoanId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(r => r.RequestedBy).WithMany().HasForeignKey(r => r.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(r => r.ApprovedBy).WithMany().HasForeignKey(r => r.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LoanReschedule>(b =>
        {
            b.HasIndex(r => r.LoanId).HasDatabaseName("IX_LoanReschedules_LoanId");
            b.Property(r => r.ReasonCode).HasMaxLength(50);
            b.HasOne(r => r.Loan).WithMany().HasForeignKey(r => r.LoanId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(r => r.RequestedBy).WithMany().HasForeignKey(r => r.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(r => r.ApprovedBy).WithMany().HasForeignKey(r => r.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LoanWriteOff>(b =>
        {
            b.HasIndex(w => w.LoanId).IsUnique().HasDatabaseName("IX_LoanWriteOffs_LoanId"); // 1 loan = 1 write-off
            b.Property(w => w.WriteOffAmount).HasPrecision(18, 2);
            b.Property(w => w.UnpaidPrincipal).HasPrecision(18, 2);
            b.Property(w => w.UnpaidInterest).HasPrecision(18, 2);
            b.Property(w => w.UnpaidLateFees).HasPrecision(18, 2);
            b.Property(w => w.ReasonCode).HasMaxLength(50);
            b.HasOne(w => w.Loan).WithMany().HasForeignKey(w => w.LoanId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(w => w.RequestedBy).WithMany().HasForeignKey(w => w.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(w => w.ApprovedBy).WithMany().HasForeignKey(w => w.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LoanRecovery>(b =>
        {
            b.HasIndex(r => r.LoanId).HasDatabaseName("IX_LoanRecoveries_LoanId");
            b.Property(r => r.RecoveredAmount).HasPrecision(18, 2);
            b.Property(r => r.RecoveryMethod).HasMaxLength(50);
            b.HasOne(r => r.Loan).WithMany().HasForeignKey(r => r.LoanId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(r => r.WriteOff).WithMany(w => w.Recoveries).HasForeignKey(r => r.WriteOffId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(r => r.RecordedBy).WithMany().HasForeignKey(r => r.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── Phase 3: Bank-Grade Accounting ──────────────────────────────
        modelBuilder.Entity<GLAccount>(b =>
        {
            b.HasIndex(a => a.AccountCode).IsUnique().HasDatabaseName("IX_GLAccounts_AccountCode");
            b.Property(a => a.AccountCode).HasMaxLength(20);
            b.Property(a => a.AccountName).HasMaxLength(100);
            b.Property(a => a.AccountType).HasMaxLength(20);
            b.Property(a => a.Balance).HasPrecision(18, 2);
        });

        modelBuilder.Entity<JournalEntry>(b =>
        {
            b.Property(j => j.ReferenceNumber).HasMaxLength(50);
            b.Property(j => j.Description).HasMaxLength(255);
            b.HasIndex(j => j.ReferenceNumber);
        });

        modelBuilder.Entity<JournalEntryLine>(b =>
        {
            b.Property(l => l.DebitAmount).HasPrecision(18, 2);
            b.Property(l => l.CreditAmount).HasPrecision(18, 2);
            b.Property(l => l.LineDescription).HasMaxLength(255);
            b.HasOne(l => l.JournalEntry).WithMany(j => j.Lines).HasForeignKey(l => l.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(l => l.GLAccount).WithMany().HasForeignKey(l => l.GLAccountId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── Phase 4: Enterprise Banking ──────────────────────────────────
        modelBuilder.Entity<ClassificationSnapshot>(b =>
        {
            b.HasIndex(s => new { s.LoanId, s.SnapshotDate }).HasDatabaseName("IX_ClassificationSnapshots_LoanId_Date");
            b.Property(s => s.OutstandingAmount).HasPrecision(18, 2);
            b.Property(s => s.ProvisionAmount).HasPrecision(18, 2);
            b.HasOne(s => s.Loan).WithMany().HasForeignKey(s => s.LoanId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccountingPeriod>(b =>
        {
            b.HasIndex(p => new { p.Year, p.Month }).IsUnique().HasDatabaseName("IX_AccountingPeriods_Year_Month");
            b.Property(p => p.Status).HasMaxLength(20);
            b.HasOne(p => p.ClosedBy).WithMany().HasForeignKey(p => p.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CicInquiry>(b =>
        {
            b.HasIndex(c => c.CustomerId).HasDatabaseName("IX_CicInquiries_CustomerId");
            b.Property(c => c.CicGrade).HasMaxLength(10);
            b.Property(c => c.Status).HasMaxLength(20);
            b.Property(c => c.Provider).HasMaxLength(20);
            b.Property(c => c.TotalBorrowingAtCic).HasPrecision(18, 2);
            b.HasOne(c => c.Customer).WithMany().HasForeignKey(c => c.CustomerId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(c => c.RequestedBy).WithMany().HasForeignKey(c => c.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EkycVerification>(b =>
        {
            b.HasIndex(e => e.CustomerId).HasDatabaseName("IX_EkycVerifications_CustomerId");
            b.Property(e => e.IdType).HasMaxLength(20);
            b.Property(e => e.IdNumber).HasMaxLength(30);
            b.Property(e => e.Status).HasMaxLength(20);
            b.Property(e => e.Provider).HasMaxLength(20);
            b.Property(e => e.FaceMatchScore).HasPrecision(5, 1);
            b.Property(e => e.OcrConfidence).HasPrecision(5, 1);
            b.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(e => e.RequestedBy).WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Branch>(b =>
        {
            b.HasIndex(br => br.BranchCode).IsUnique().HasDatabaseName("IX_Branches_BranchCode");
            b.Property(br => br.BranchCode).HasMaxLength(20);
            b.Property(br => br.BranchName).HasMaxLength(100);
            b.Property(br => br.Status).HasMaxLength(20);
            b.HasOne(br => br.Manager).WithMany().HasForeignKey(br => br.ManagerUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LoanFlowDbContext).Assembly);
    }
}
