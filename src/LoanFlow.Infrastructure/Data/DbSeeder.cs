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

        // ── Users — passwords from env vars or generated at first run ─────────
        static string ResolvePassword(string envKey)
        {
            var fromEnv = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;
            // Generate a cryptographically random 16-char password
            var bytes = new byte[12];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes); // ~16 printable chars
        }

        var adminPass    = ResolvePassword("SEED_ADMIN_PASSWORD");
        var officerPass  = ResolvePassword("SEED_OFFICER_PASSWORD");
        var approverPass = ResolvePassword("SEED_APPROVER_PASSWORD");
        var collectorPass= ResolvePassword("SEED_COLLECTOR_PASSWORD");
        var managerPass  = ResolvePassword("SEED_MANAGER_PASSWORD");

        var adminHash    = BCrypt.Net.BCrypt.HashPassword(adminPass);
        var officerHash  = BCrypt.Net.BCrypt.HashPassword(officerPass);
        var approverHash = BCrypt.Net.BCrypt.HashPassword(approverPass);
        var collectorHash= BCrypt.Net.BCrypt.HashPassword(collectorPass);
        var managerHash  = BCrypt.Net.BCrypt.HashPassword(managerPass);

        Console.WriteLine("[Seeder] ============================================");
        Console.WriteLine($"[Seeder] admin       password: {adminPass}");
        Console.WriteLine($"[Seeder] officer01   password: {officerPass}");
        Console.WriteLine($"[Seeder] approver01  password: {approverPass}");
        Console.WriteLine($"[Seeder] collector01 password: {collectorPass}");
        Console.WriteLine($"[Seeder] manager01   password: {managerPass}");
        Console.WriteLine("[Seeder] ============================================");
        Console.WriteLine("[Seeder] Save the above passwords — they are hashed and cannot be recovered.");

        var admin     = new User { Username = "admin",       PasswordHash = adminHash,     FullName = "Quản trị hệ thống", Role = Roles.Admin };
        var officer   = new User { Username = "officer01",   PasswordHash = officerHash,   FullName = "Nguyễn Văn An",     Role = Roles.LoanOfficer };
        var approver  = new User { Username = "approver01",  PasswordHash = approverHash,  FullName = "Trần Thị Bình",     Role = Roles.Approver };
        var collector = new User { Username = "collector01", PasswordHash = collectorHash, FullName = "Lê Minh Cường",     Role = Roles.Collector };
        var manager   = new User { Username = "manager01",   PasswordHash = managerHash,   FullName = "Phạm Anh Dũng",     Role = Roles.Manager };
        db.Users.AddRange(admin, officer, approver, collector, manager);
        await db.SaveChangesAsync();

        // ── Loan Products (§22) ────────────────────────────────────────────────
        var products = new List<LoanProduct>
        {
            new() {
                ProductName = "Installment 6M",
                MinAmount = 5_000_000, MaxAmount = 20_000_000,
                AllowedTenorsJson = JsonSerializer.Serialize(new[] { 6 }),
                MonthlyInterestRate = 0.015m,   // 1.5%
                ProcessingFee = 200_000,
                Status = "Active"
            },
            new() {
                ProductName = "Installment 12M",
                MinAmount = 5_000_000, MaxAmount = 50_000_000,
                AllowedTenorsJson = JsonSerializer.Serialize(new[] { 12 }),
                MonthlyInterestRate = 0.015m,   // 1.5%
                ProcessingFee = 300_000,
                Status = "Active"
            },
            new() {
                ProductName = "Installment 18M",
                MinAmount = 10_000_000, MaxAmount = 100_000_000,
                AllowedTenorsJson = JsonSerializer.Serialize(new[] { 18 }),
                MonthlyInterestRate = 0.017m,   // 1.7%
                ProcessingFee = 500_000,
                Status = "Active"
            }
        };
        db.LoanProducts.AddRange(products);
        await db.SaveChangesAsync();

        // ── Customers (§22 — 10 valid customers) ──────────────────────────────
        var customers = new List<Customer>
        {
            new() { FullName = "Nguyễn Văn A",   Phone = "0901111001", IdNumber = "079100000001", Address = "123 Nguyễn Huệ, Q1, TP.HCM",  DateOfBirth = new DateTime(1990,1,15),  CreatedByUserId = officer.Id },
            new() { FullName = "Trần Thị B",     Phone = "0901111002", IdNumber = "079100000002", Address = "45 Lê Lợi, Q1, TP.HCM",       DateOfBirth = new DateTime(1992,4,20),  CreatedByUserId = officer.Id },
            new() { FullName = "Lê Văn C",       Phone = "0901111003", IdNumber = "079100000003", Address = "78 Hai Bà Trưng, Q3, TP.HCM",  DateOfBirth = new DateTime(1988,7,10),  CreatedByUserId = officer.Id },
            new() { FullName = "Phạm Thị D",     Phone = "0901111004", IdNumber = "079100000004", Address = "12 Điện Biên Phủ, Q10, TP.HCM", DateOfBirth = new DateTime(1995,3,5),  CreatedByUserId = officer.Id },
            new() { FullName = "Hoàng Văn E",    Phone = "0901111005", IdNumber = "079100000005", Address = "99 Cách Mạng Tháng 8, Q3",      DateOfBirth = new DateTime(1985,11,25), CreatedByUserId = officer.Id },
            new() { FullName = "Vũ Thị F",       Phone = "0901111006", IdNumber = "079100000006", Address = "33 Ngô Đức Kế, Q1, TP.HCM",    DateOfBirth = new DateTime(1993,8,18),  CreatedByUserId = officer.Id },
            new() { FullName = "Đặng Văn G",     Phone = "0901111007", IdNumber = "079100000007", Address = "5 Mạc Thị Bưởi, Q1, TP.HCM",   DateOfBirth = new DateTime(1987,2,12),  CreatedByUserId = officer.Id },
            new() { FullName = "Bùi Thị H",      Phone = "0901111008", IdNumber = "079100000008", Address = "18 Pasteur, Q3, TP.HCM",         DateOfBirth = new DateTime(1991,9,30),  CreatedByUserId = officer.Id },
            new() { FullName = "Ngô Văn I",      Phone = "0901111009", IdNumber = "079100000009", Address = "7 Lý Tự Trọng, Q1, TP.HCM",     DateOfBirth = new DateTime(1989,6,22),  CreatedByUserId = officer.Id },
            new() { FullName = "Đỗ Thị K",       Phone = "0901111010", IdNumber = "079100000010", Address = "21 Nam Kỳ Khởi Nghĩa, Q3",       DateOfBirth = new DateTime(1994,12,8),  CreatedByUserId = officer.Id },
        };
        db.Customers.AddRange(customers);
        await db.SaveChangesAsync();

        // ── Demo happy-path: Nguyễn Văn A, Installment 12M, 20M VND (§30 Scenario A) ──
        var demoCustomer = customers[0];
        var demoProd = products[1]; // Installment 12M

        // Application: APPROVED
        var demoApp = new LoanApplication
        {
            CustomerId = demoCustomer.Id,
            ProductId = demoProd.Id,
            RequestedAmount = 20_000_000,
            RequestedTenorMonths = 12,
            Purpose = "Laptop installment",
            Remarks = "Demo application",
            Status = ApplicationStatus.Approved,
            CreatedByUserId = officer.Id,
            SubmittedAt = DateTime.UtcNow.AddDays(-5)
        };
        db.LoanApplications.Add(demoApp);
        await db.SaveChangesAsync();

        db.ApplicationDecisions.Add(new ApplicationDecision
        {
            ApplicationId = demoApp.Id,
            Decision = "Approved",
            DecidedByUserId = approver.Id
        });

        // Loan: ACTIVE — disbursed
        var disbursementDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-35));
        var demoLoan = new Loan
        {
            LoanAccountNo = "LN-2026-0001",
            ApplicationId = demoApp.Id,
            CustomerId = demoCustomer.Id,
            ProductId = demoProd.Id,
            ApprovedAmount = 20_000_000,
            TenorMonths = 12,
            MonthlyInterestRate = demoProd.MonthlyInterestRate,
            ProcessingFee = demoProd.ProcessingFee,
            DisbursementDate = disbursementDate,
            MaturityDate = disbursementDate.AddMonths(12),
            Status = LoanStatus.Active,
            OutstandingAmount = 18_333_333.34m  // after first payment
        };
        db.Loans.Add(demoLoan);
        await db.SaveChangesAsync();

        db.LoanDisbursements.Add(new LoanDisbursement
        {
            LoanId = demoLoan.Id,
            DisbursedAmount = 20_000_000,
            DisbursementDate = disbursementDate,
            RecordedByUserId = officer.Id
        });

        db.LoanTransactions.Add(new LoanTransaction
        {
            LoanId = demoLoan.Id,
            TransactionType = "Disbursement",
            Amount = 20_000_000
        });

        // Repayment schedule — flat model: principal = 20M/12, interest = 20M * 1.5%
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
                LoanId = demoLoan.Id,
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

        // First payment recorded
        var firstPayment = new Payment
        {
            LoanId = demoLoan.Id,
            PaymentDate = disbursementDate.AddMonths(1),
            Amount = principalPerInstallment + interestPerInstallment,
            PaymentMethod = PaymentMethod.Cash,
            Remarks = "First installment payment",
            CreatedByUserId = officer.Id
        };
        db.Payments.Add(firstPayment);
        await db.SaveChangesAsync();

        db.LoanTransactions.Add(new LoanTransaction
        {
            LoanId = demoLoan.Id,
            PaymentId = firstPayment.Id,
            TransactionType = "Payment",
            Amount = firstPayment.Amount
        });

        // ── Overdue demo loan (§30 Scenario B) ────────────────────────────────
        var overdueCustomer = customers[2]; // Lê Văn C
        var overdueApp = new LoanApplication
        {
            CustomerId = overdueCustomer.Id,
            ProductId = products[0].Id, // 6M
            RequestedAmount = 10_000_000,
            RequestedTenorMonths = 6,
            Purpose = "Working capital",
            Status = ApplicationStatus.Approved,
            CreatedByUserId = officer.Id,
            SubmittedAt = DateTime.UtcNow.AddDays(-75)
        };
        db.LoanApplications.Add(overdueApp);
        await db.SaveChangesAsync();

        db.ApplicationDecisions.Add(new ApplicationDecision
        {
            ApplicationId = overdueApp.Id,
            Decision = "Approved",
            DecidedByUserId = approver.Id
        });

        var overdueDisb = DateOnly.FromDateTime(DateTime.Today.AddDays(-65));
        var overdueLoan = new Loan
        {
            LoanAccountNo = "LN-2026-0002",
            ApplicationId = overdueApp.Id,
            CustomerId = overdueCustomer.Id,
            ProductId = products[0].Id,
            ApprovedAmount = 10_000_000,
            TenorMonths = 6,
            MonthlyInterestRate = 0.015m,
            ProcessingFee = 200_000,
            DisbursementDate = overdueDisb,
            MaturityDate = overdueDisb.AddMonths(6),
            Status = LoanStatus.Overdue,
            OutstandingAmount = 10_000_000m
        };
        db.Loans.Add(overdueLoan);
        await db.SaveChangesAsync();

        db.LoanDisbursements.Add(new LoanDisbursement
        {
            LoanId = overdueLoan.Id,
            DisbursedAmount = 10_000_000,
            DisbursementDate = overdueDisb,
            RecordedByUserId = officer.Id
        });
        db.LoanTransactions.Add(new LoanTransaction
        {
            LoanId = overdueLoan.Id,
            TransactionType = "Disbursement",
            Amount = 10_000_000
        });

        // All installments overdue — no payment made
        var overduePrincipal = Math.Round(10_000_000m / 6, 2);
        var overdueLastPrincipal = 10_000_000m - overduePrincipal * 5;
        var overdueInterest = Math.Round(10_000_000m * 0.015m, 2);
        var overdueSchedules = new List<RepaymentSchedule>();
        for (int i = 1; i <= 6; i++)
        {
            var p = i == 6 ? overdueLastPrincipal : overduePrincipal;
            overdueSchedules.Add(new RepaymentSchedule
            {
                LoanId = overdueLoan.Id,
                InstallmentNo = i,
                DueDate = overdueDisb.AddMonths(i),
                PrincipalDue = p,
                InterestDue = overdueInterest,
                PrincipalPaid = 0,
                InterestPaid = 0,
                RemainingDueAmount = p + overdueInterest,
                Status = InstallmentStatus.Overdue  // all overdue
            });
        }
        db.RepaymentSchedules.AddRange(overdueSchedules);

        // Collection note on overdue loan
        db.CollectionNotes.Add(new CollectionNote
        {
            LoanId = overdueLoan.Id,
            NoteDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-5)),
            NoteText = "Khách hàng hứa thanh toán vào cuối tuần tới",
            CreatedByUserId = collector.Id
        });

        await db.SaveChangesAsync();
        Console.WriteLine("[Seeder v2] Done: Users, Products, Customers, Demo loan, Overdue loan seeded.");
    }
}
