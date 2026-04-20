using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LoanFlow.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(LoanFlowDbContext db)
    {
        if (await db.Users.AnyAsync()) return; // Already seeded

        await SeedGLAccountsAsync(db);

        // Phase 4: Seed branches first (users reference them)
        var branches = await SeedBranchesAsync(db);

        var users = await SeedUsersAsync(db);
        var products = await SeedLoanProductsAsync(db);
        var customers = await SeedCustomersAsync(db, users.Officer.Id);

        // Phase 4: Assign users and customers to branches
        await AssignBranchesAsync(db, users, customers, branches);

        var happyLoan = await SeedHappyPathScenarioAsync(
            db,
            customers[0],
            products[1],
            users.Officer.Id,
            users.Approver.Id);

        await SeedOverdueScenarioAsync(
            db,
            customers[2],
            products[0],
            users.Officer.Id,
            users.Approver.Id,
            users.Collector.Id);

        // ── Phase 1: Maker-Checker & Manual Adjustment seed data ────────────
        await SeedMakerCheckerScenariosAsync(db, happyLoan.Id, users.Officer.Id, users.Manager.Id);

        await SeedAccountingPeriodAsync(db);
        
        // EOD Lock Seed
        await SeedSystemConfigAsync(db);

        Console.WriteLine("[Seeder v4] Done: GL Accounts, Branches, Users, Products, Customers, Demo loans, Phase 1-4 scenarios, configs seeded.");
    }

    private static async Task SeedSystemConfigAsync(LoanFlowDbContext db)
    {
        if (!await db.SystemConfigs.AnyAsync(s => s.Key == "IsEodRunning"))
        {
            db.SystemConfigs.Add(new SystemConfig
            {
                Key = "IsEodRunning",
                Value = "false",
                Description = "True if End-Of-Day batch is running. Blocks external transactions.",
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedGLAccountsAsync(LoanFlowDbContext db)
    {
        var accounts = new List<GLAccount>
        {
            new GLAccount { AccountCode = "1001", AccountName = "Cash in Bank", AccountType = GLAccountType.Asset },
            new GLAccount { AccountCode = "1301", AccountName = "Loan Principal Outstanding", AccountType = GLAccountType.Asset },
            new GLAccount { AccountCode = "1302", AccountName = "Interest Receivable", AccountType = GLAccountType.Asset },
            new GLAccount { AccountCode = "1303", AccountName = "Late Fee Receivable", AccountType = GLAccountType.Asset },
            new GLAccount { AccountCode = "1309", AccountName = "Allowance for Loan Losses", AccountType = GLAccountType.Asset }, // Contra-asset
            
            new GLAccount { AccountCode = "2001", AccountName = "Suspense / Overpayment", AccountType = GLAccountType.Liability },
            
            new GLAccount { AccountCode = "4001", AccountName = "Interest Income", AccountType = GLAccountType.Income },
            new GLAccount { AccountCode = "4002", AccountName = "Late Fee Income", AccountType = GLAccountType.Income },
            new GLAccount { AccountCode = "4004", AccountName = "Early Settle Fee Income", AccountType = GLAccountType.Income },
            new GLAccount { AccountCode = "4005", AccountName = "Recovery Income", AccountType = GLAccountType.Income },
            
            new GLAccount { AccountCode = "5001", AccountName = "Provision Expense", AccountType = GLAccountType.Expense },
            new GLAccount { AccountCode = "5002", AccountName = "Write-Off Expense", AccountType = GLAccountType.Expense },
        };
        db.GLAccounts.AddRange(accounts);
        await db.SaveChangesAsync();
    }

    private static async Task<SeedUsersResult> SeedUsersAsync(LoanFlowDbContext db)
    {
        var adminPass = ResolvePassword("SEED_ADMIN_PASSWORD");
        var officerPass = ResolvePassword("SEED_OFFICER_PASSWORD");
        var approverPass = ResolvePassword("SEED_APPROVER_PASSWORD");
        var collectorPass = ResolvePassword("SEED_COLLECTOR_PASSWORD");
        var managerPass = ResolvePassword("SEED_MANAGER_PASSWORD");

        PrintSeedPasswords(adminPass, officerPass, approverPass, collectorPass, managerPass);

        var admin = new User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPass),
            FullName = "Quản trị hệ thống",
            Role = Roles.Admin
        };
        var officer = new User
        {
            Username = "officer01",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(officerPass),
            FullName = "Nguyễn Văn An",
            Role = Roles.LoanOfficer
        };
        var approver = new User
        {
            Username = "approver01",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(approverPass),
            FullName = "Trần Thị Bình",
            Role = Roles.Approver
        };
        var collector = new User
        {
            Username = "collector01",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(collectorPass),
            FullName = "Lê Minh Cường",
            Role = Roles.Collector
        };
        var manager = new User
        {
            Username = "manager01",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(managerPass),
            FullName = "Phạm Anh Dũng",
            Role = Roles.Manager
        };

        db.Users.AddRange(admin, officer, approver, collector, manager);
        await db.SaveChangesAsync();

        return new SeedUsersResult(admin, officer, approver, collector, manager);
    }

    private static async Task<List<LoanProduct>> SeedLoanProductsAsync(LoanFlowDbContext db)
    {
        var products = new List<LoanProduct>
        {
            new()
            {
                ProductName = "Installment 6M",
                MinAmount = 5_000_000,
                MaxAmount = 20_000_000,
                AllowedTenorsJson = JsonSerializer.Serialize(new[] { 6 }),
                MonthlyInterestRate = 0.015m,
                ProcessingFee = 200_000,
                Status = "Active"
            },
            new()
            {
                ProductName = "Installment 12M",
                MinAmount = 5_000_000,
                MaxAmount = 50_000_000,
                AllowedTenorsJson = JsonSerializer.Serialize(new[] { 12 }),
                MonthlyInterestRate = 0.015m,
                ProcessingFee = 300_000,
                Status = "Active"
            },
            new()
            {
                ProductName = "Installment 18M",
                MinAmount = 10_000_000,
                MaxAmount = 100_000_000,
                AllowedTenorsJson = JsonSerializer.Serialize(new[] { 18 }),
                MonthlyInterestRate = 0.017m,
                ProcessingFee = 500_000,
                Status = "Active"
            }
        };

        db.LoanProducts.AddRange(products);
        await db.SaveChangesAsync();
        return products;
    }

    private static async Task<List<Customer>> SeedCustomersAsync(LoanFlowDbContext db, int createdByUserId)
    {
        var customers = new List<Customer>
        {
            new() { FullName = "Nguyễn Văn A", Phone = "0901111001", IdNumber = "079100000001", Address = "123 Nguyễn Huệ, Q1, TP.HCM", DateOfBirth = new DateTime(1990, 1, 15), CreatedByUserId = createdByUserId },
            new() { FullName = "Trần Thị B", Phone = "0901111002", IdNumber = "079100000002", Address = "45 Lê Lợi, Q1, TP.HCM", DateOfBirth = new DateTime(1992, 4, 20), CreatedByUserId = createdByUserId },
            new() { FullName = "Lê Văn C", Phone = "0901111003", IdNumber = "079100000003", Address = "78 Hai Bà Trưng, Q3, TP.HCM", DateOfBirth = new DateTime(1988, 7, 10), CreatedByUserId = createdByUserId },
            new() { FullName = "Phạm Thị D", Phone = "0901111004", IdNumber = "079100000004", Address = "12 Điện Biên Phủ, Q10, TP.HCM", DateOfBirth = new DateTime(1995, 3, 5), CreatedByUserId = createdByUserId },
            new() { FullName = "Hoàng Văn E", Phone = "0901111005", IdNumber = "079100000005", Address = "99 Cách Mạng Tháng 8, Q3", DateOfBirth = new DateTime(1985, 11, 25), CreatedByUserId = createdByUserId },
            new() { FullName = "Vũ Thị F", Phone = "0901111006", IdNumber = "079100000006", Address = "33 Ngô Đức Kế, Q1, TP.HCM", DateOfBirth = new DateTime(1993, 8, 18), CreatedByUserId = createdByUserId },
            new() { FullName = "Đặng Văn G", Phone = "0901111007", IdNumber = "079100000007", Address = "5 Mạc Thị Bưởi, Q1, TP.HCM", DateOfBirth = new DateTime(1987, 2, 12), CreatedByUserId = createdByUserId },
            new() { FullName = "Bùi Thị H", Phone = "0901111008", IdNumber = "079100000008", Address = "18 Pasteur, Q3, TP.HCM", DateOfBirth = new DateTime(1991, 9, 30), CreatedByUserId = createdByUserId },
            new() { FullName = "Ngô Văn I", Phone = "0901111009", IdNumber = "079100000009", Address = "7 Lý Tự Trọng, Q1, TP.HCM", DateOfBirth = new DateTime(1989, 6, 22), CreatedByUserId = createdByUserId },
            new() { FullName = "Đỗ Thị K", Phone = "0901111010", IdNumber = "079100000010", Address = "21 Nam Kỳ Khởi Nghĩa, Q3", DateOfBirth = new DateTime(1994, 12, 8), CreatedByUserId = createdByUserId }
        };

        db.Customers.AddRange(customers);
        await db.SaveChangesAsync();
        return customers;
    }

    private static async Task<Loan> SeedHappyPathScenarioAsync(
        LoanFlowDbContext db,
        Customer customer,
        LoanProduct product,
        int officerUserId,
        int approverUserId)
    {
        var application = await CreateApprovedApplicationAsync(
            db,
            customer.Id,
            product.Id,
            20_000_000,
            12,
            "Laptop installment",
            "Demo application",
            DateTime.UtcNow.AddDays(-5),
            officerUserId,
            approverUserId);

        var disbursementDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-35));

        var loan = await CreateLoanAsync(
            db,
            "LN-2026-0001",
            application.Id,
            customer.Id,
            product.Id,
            20_000_000,
            12,
            product.MonthlyInterestRate,
            product.ProcessingFee,
            disbursementDate,
            disbursementDate.AddMonths(12),
            LoanStatus.Active,
            18_333_333.34m);

        await AddDisbursementAsync(db, loan.Id, 20_000_000, disbursementDate, officerUserId);

        var principalPerInstallment = Math.Round(20_000_000m / 12, 2);
        var lastPrincipal = 20_000_000m - principalPerInstallment * 11;
        var interestPerInstallment = Math.Round(20_000_000m * 0.015m, 2);

        var schedules = new List<RepaymentSchedule>();
        for (int i = 1; i <= 12; i++)
        {
            var principal = i == 12 ? lastPrincipal : principalPerInstallment;
            var remaining = principal + interestPerInstallment;

            schedules.Add(new RepaymentSchedule
            {
                LoanId = loan.Id,
                InstallmentNo = i,
                DueDate = disbursementDate.AddMonths(i),
                PrincipalDue = principal,
                InterestDue = interestPerInstallment,
                PrincipalPaid = i == 1 ? principal : 0,
                InterestPaid = i == 1 ? interestPerInstallment : 0,
                RemainingDueAmount = i == 1 ? 0 : remaining,
                Status = i == 1 ? InstallmentStatus.Paid : InstallmentStatus.Unpaid
            });
        }

        db.RepaymentSchedules.AddRange(schedules);

        var firstPayment = new Payment
        {
            LoanId = loan.Id,
            PaymentDate = disbursementDate.AddMonths(1),
            Amount = principalPerInstallment + interestPerInstallment,
            PaymentMethod = PaymentMethod.Cash,
            Remarks = "First installment payment",
            CreatedByUserId = officerUserId
        };

        db.Payments.Add(firstPayment);
        await db.SaveChangesAsync();

        db.LoanTransactions.Add(new LoanTransaction
        {
            LoanId = loan.Id,
            PaymentId = firstPayment.Id,
            TransactionType = "Payment",
            Amount = firstPayment.Amount
        });

        await db.SaveChangesAsync();
        return loan;
    }

    private static async Task SeedOverdueScenarioAsync(
        LoanFlowDbContext db,
        Customer customer,
        LoanProduct product,
        int officerUserId,
        int approverUserId,
        int collectorUserId)
    {
        var application = await CreateApprovedApplicationAsync(
            db,
            customer.Id,
            product.Id,
            10_000_000,
            6,
            "Working capital",
            null,
            DateTime.UtcNow.AddDays(-75),
            officerUserId,
            approverUserId);

        var disbursementDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-65));

        var loan = await CreateLoanAsync(
            db,
            "LN-2026-0002",
            application.Id,
            customer.Id,
            product.Id,
            10_000_000,
            6,
            0.015m,
            200_000,
            disbursementDate,
            disbursementDate.AddMonths(6),
            LoanStatus.Overdue,
            10_000_000m);

        await AddDisbursementAsync(db, loan.Id, 10_000_000, disbursementDate, officerUserId);

        var overduePrincipal = Math.Round(10_000_000m / 6, 2);
        var overdueLastPrincipal = 10_000_000m - overduePrincipal * 5;
        var overdueInterest = Math.Round(10_000_000m * 0.015m, 2);
        var schedules = new List<RepaymentSchedule>();

        for (int i = 1; i <= 6; i++)
        {
            var principal = i == 6 ? overdueLastPrincipal : overduePrincipal;
            schedules.Add(new RepaymentSchedule
            {
                LoanId = loan.Id,
                InstallmentNo = i,
                DueDate = disbursementDate.AddMonths(i),
                PrincipalDue = principal,
                InterestDue = overdueInterest,
                PrincipalPaid = 0,
                InterestPaid = 0,
                RemainingDueAmount = principal + overdueInterest,
                Status = InstallmentStatus.Overdue
            });
        }

        db.RepaymentSchedules.AddRange(schedules);
        db.CollectionNotes.Add(new CollectionNote
        {
            LoanId = loan.Id,
            NoteDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-5)),
            NoteText = "Khách hàng hứa thanh toán vào cuối tuần tới",
            CreatedByUserId = collectorUserId
        });

        await db.SaveChangesAsync();
    }

    // ── Phase 1: Maker-Checker seed scenarios ──────────────────────────────
    private static async Task SeedMakerCheckerScenariosAsync(
        LoanFlowDbContext db,
        int loanId,
        int makerUserId,
        int checkerUserId)
    {
        // Scenario 1: Pending ManualAdjustment request (waiting for Checker approval)
        // Demonstrates the approval queue in the UI
        var pendingPayload = System.Text.Json.JsonSerializer.Serialize(new
        {
            LoanId = loanId,
            AdjustmentType = "LateFee",
            Amount = -150_000m,
            ReasonCode = "CustomerHardship",
            Note = "Miễn giảm phí trễ hạn theo yêu cầu của khách hàng do hoàn cảnh khó khăn"
        });

        db.PendingApprovalRequests.Add(new PendingApprovalRequest
        {
            ActionType = "ManualAdjustment",
            Payload = pendingPayload,
            RequestedByUserId = makerUserId,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        });

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = makerUserId,
            ActionCode = "ApprovalRequestSubmitted",
            EntityType = "PendingApprovalRequest",
            NewValuesJson = System.Text.Json.JsonSerializer.Serialize(new { actionType = "ManualAdjustment", loanId })
        });

        await db.SaveChangesAsync();

        // Scenario 2: Approved ManualAdjustment — interest waiver already applied
        // Demonstrates the completed adjustment history
        var approvedPayload = System.Text.Json.JsonSerializer.Serialize(new
        {
            LoanId = loanId,
            AdjustmentType = "Interest",
            Amount = -300_000m,
            ReasonCode = "SystemError",
            Note = "Điều chỉnh lãi do lỗi tính lãi kép sai trong kỳ 2"
        });

        var approvedRequest = new PendingApprovalRequest
        {
            ActionType = "ManualAdjustment",
            Payload = approvedPayload,
            RequestedByUserId = makerUserId,
            ApprovedByUserId = checkerUserId,
            Status = "Approved",
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            DecidedAt = DateTime.UtcNow.AddDays(-3).AddHours(1)
        };
        db.PendingApprovalRequests.Add(approvedRequest);
        await db.SaveChangesAsync();

        // Create the actual LoanAdjustment record (what would be created by ApprovalFlowService)
        db.LoanAdjustments.Add(new LoanAdjustment
        {
            LoanId = loanId,
            AdjustmentType = "Interest",
            Amount = -300_000m,
            ReasonCode = "SystemError",
            Note = "Điều chỉnh lãi do lỗi tính lãi kép sai trong kỳ 2",
            CreatedByUserId = makerUserId,
            ApprovedByUserId = checkerUserId,
            AppliedAt = DateTime.UtcNow.AddDays(-3).AddHours(1)
        });

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = checkerUserId,
            ActionCode = "ApprovalRequestApproved",
            EntityType = "PendingApprovalRequest",
            EntityId = approvedRequest.Id,
            NewValuesJson = System.Text.Json.JsonSerializer.Serialize(new { loanId, amount = -300_000m, type = "Interest" })
        });

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = checkerUserId,
            ActionCode = "LoanAdjustmentApplied",
            EntityType = "Loan",
            EntityId = loanId,
            NewValuesJson = System.Text.Json.JsonSerializer.Serialize(new { amount = -300_000m, type = "Interest" })
        });

        // Scenario 3: Rejected request — fee waive denied
        var rejectedPayload = System.Text.Json.JsonSerializer.Serialize(new
        {
            LoanId = loanId,
            AdjustmentType = "Fee",
            Amount = -200_000m,
            ReasonCode = "CustomerRequest",
            Note = "Khách hàng xin miễn phí xử lý hồ sơ vì đã là khách hàng lâu năm"
        });

        db.PendingApprovalRequests.Add(new PendingApprovalRequest
        {
            ActionType = "ManualAdjustment",
            Payload = rejectedPayload,
            RequestedByUserId = makerUserId,
            ApprovedByUserId = checkerUserId,
            Status = "Rejected",
            RejectionReason = "Không đủ điều kiện miễn giảm phí theo chính sách hiện hành",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            DecidedAt = DateTime.UtcNow.AddDays(-1).AddHours(3)
        });

        await db.SaveChangesAsync();

        Console.WriteLine("[Seeder v3] Phase-1 seeded: 1 Pending, 1 Approved, 1 Rejected approval request.");
    }

    private static async Task<LoanApplication> CreateApprovedApplicationAsync(
        LoanFlowDbContext db,
        int customerId,
        int productId,
        decimal requestedAmount,
        int requestedTenorMonths,
        string? purpose,
        string? remarks,
        DateTime submittedAt,
        int createdByUserId,
        int approverUserId)
    {
        var application = new LoanApplication
        {
            CustomerId = customerId,
            ProductId = productId,
            RequestedAmount = requestedAmount,
            RequestedTenorMonths = requestedTenorMonths,
            Purpose = purpose,
            Remarks = remarks,
            Status = ApplicationStatus.Approved,
            CreatedByUserId = createdByUserId,
            SubmittedAt = submittedAt
        };

        db.LoanApplications.Add(application);
        await db.SaveChangesAsync();

        db.ApplicationDecisions.Add(new ApplicationDecision
        {
            ApplicationId = application.Id,
            Decision = "Approved",
            DecidedByUserId = approverUserId
        });

        await db.SaveChangesAsync();
        return application;
    }

    private static async Task<Loan> CreateLoanAsync(
        LoanFlowDbContext db,
        string loanAccountNo,
        int applicationId,
        int customerId,
        int productId,
        decimal approvedAmount,
        int tenorMonths,
        decimal monthlyInterestRate,
        decimal processingFee,
        DateOnly disbursementDate,
        DateOnly maturityDate,
        string status,
        decimal outstandingAmount)
    {
        var loan = new Loan
        {
            LoanAccountNo = loanAccountNo,
            ApplicationId = applicationId,
            CustomerId = customerId,
            ProductId = productId,
            ApprovedAmount = approvedAmount,
            TenorMonths = tenorMonths,
            MonthlyInterestRate = monthlyInterestRate,
            ProcessingFee = processingFee,
            DisbursementDate = disbursementDate,
            MaturityDate = maturityDate,
            Status = status,
            OutstandingAmount = outstandingAmount
        };

        db.Loans.Add(loan);
        await db.SaveChangesAsync();
        return loan;
    }

    private static async Task AddDisbursementAsync(
        LoanFlowDbContext db,
        int loanId,
        decimal disbursedAmount,
        DateOnly disbursementDate,
        int recordedByUserId)
    {
        db.LoanDisbursements.Add(new LoanDisbursement
        {
            LoanId = loanId,
            DisbursedAmount = disbursedAmount,
            DisbursementDate = disbursementDate,
            RecordedByUserId = recordedByUserId
        });

        db.LoanTransactions.Add(new LoanTransaction
        {
            LoanId = loanId,
            TransactionType = "Disbursement",
            Amount = disbursedAmount
        });

        await db.SaveChangesAsync();
    }

    private static string ResolvePassword(string envKey)
    {
        var fromEnv = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;

        var bytes = new byte[12];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static void PrintSeedPasswords(
        string adminPass,
        string officerPass,
        string approverPass,
        string collectorPass,
        string managerPass)
    {
        Console.WriteLine("[Seeder] ============================================");
        Console.WriteLine($"[Seeder] admin       password: {adminPass}");
        Console.WriteLine($"[Seeder] officer01   password: {officerPass}");
        Console.WriteLine($"[Seeder] approver01  password: {approverPass}");
        Console.WriteLine($"[Seeder] collector01 password: {collectorPass}");
        Console.WriteLine($"[Seeder] manager01   password: {managerPass}");
        Console.WriteLine("[Seeder] ============================================");
        Console.WriteLine("[Seeder] Save the above passwords - they are hashed and cannot be recovered.");
    }

    private sealed record SeedUsersResult(
        User Admin,
        User Officer,
        User Approver,
        User Collector,
        User Manager);

    // ── Phase 4: Branches ───────────────────────────────────────────────────
    private static async Task<List<Branch>> SeedBranchesAsync(LoanFlowDbContext db)
    {
        var branches = new List<Branch>
        {
            new() { BranchCode = "HCM-HQ", BranchName = "HCM - Hội sở chính", Address = "123 Nguyễn Huệ, Q.1, TP.HCM", Phone = "028-1234-5678" },
            new() { BranchCode = "HN-01", BranchName = "HN - Chi nhánh Hà Nội", Address = "45 Hoàn Kiếm, Hà Nội", Phone = "024-9876-5432" },
        };

        db.Branches.AddRange(branches);
        await db.SaveChangesAsync();
        Console.WriteLine("[Seeder] 2 branches seeded (HCM-HQ, HN-01).");
        return branches;
    }

    private static async Task AssignBranchesAsync(LoanFlowDbContext db, SeedUsersResult users, List<Customer> customers, List<Branch> branches)
    {
        var hcm = branches[0];  // HCM-HQ
        var hn = branches[1];   // HN-01

        // Admin stays null (HQ, sees everything)
        users.Officer.BranchId = hcm.Id;
        users.Approver.BranchId = hcm.Id;
        users.Collector.BranchId = hcm.Id;
        users.Manager.BranchId = hcm.Id;

        // Assign manager to branch
        hcm.ManagerUserId = users.Manager.Id;

        // Assign customers to HCM branch
        foreach (var c in customers)
            c.BranchId = hcm.Id;

        await db.SaveChangesAsync();
    }

    private static async Task SeedAccountingPeriodAsync(LoanFlowDbContext db)
    {
        var now = DateTime.UtcNow;
        db.AccountingPeriods.Add(new AccountingPeriod
        {
            Year = now.Year,
            Month = now.Month,
            Status = "Open",
            OpenedAt = now
        });
        await db.SaveChangesAsync();
        Console.WriteLine($"[Seeder] Accounting period {now.Year}/{now.Month:D2} opened.");
    }
}
