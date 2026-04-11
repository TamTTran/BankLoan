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

    // ── Bank-grade entities ─────────────────────────────────────────────────
    public DbSet<LateFee> LateFees => Set<LateFee>();
    public DbSet<Collateral> Collaterals => Set<Collateral>();
    public DbSet<LoanCollateral> LoanCollaterals => Set<LoanCollateral>();
    public DbSet<ApprovalRule> ApprovalRules => Set<ApprovalRule>();
    public DbSet<CreditCheck> CreditChecks => Set<CreditCheck>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LoanFlowDbContext).Assembly);
    }
}
