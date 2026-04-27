using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LoanFlow.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(LoanFlowDbContext db)
    {
        if (await db.Users.AnyAsync())
        {
            await SeedTestPortfolioAsync(db);
            return;
        }

        await SeedGLAccountsAsync(db);
        await SeedGLAccountMappingsAsync(db);
        await SeedPermissionsAsync(db);

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
        await SeedTestPortfolioAsync(db);
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

    private static async Task SeedTestPortfolioAsync(LoanFlowDbContext db)
    {
        const int targetCount = 100;
        const string loanPrefix = "LN-TST-2026-";
        const string phonePrefix = "09877";

        var officer = await db.Users.FirstOrDefaultAsync(u => u.Role == Roles.LoanOfficer);
        var approver = await db.Users.FirstOrDefaultAsync(u => u.Role == Roles.Approver);
        var collector = await db.Users.FirstOrDefaultAsync(u => u.Role == Roles.Collector);
        var manager = await db.Users.FirstOrDefaultAsync(u => u.Role == Roles.Manager);
        var admin = await db.Users.FirstOrDefaultAsync(u => u.Role == Roles.Admin);
        var products = await db.LoanProducts.Where(p => p.Status == "Active").OrderBy(p => p.Id).ToListAsync();

        if (officer is null || approver is null || collector is null || manager is null || admin is null || products.Count == 0)
        {
            Console.WriteLine("[Seeder] Test portfolio skipped: base users/products are missing.");
            return;
        }

        var branchId = officer.BranchId ?? await db.Branches.Select(b => (int?)b.Id).FirstOrDefaultAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var existingCount = await db.Loans.CountAsync(l => l.LoanAccountNo.StartsWith(loanPrefix));

        if (existingCount >= targetCount)
        {
            await EnsureTestPortfolioSupportDataAsync(db, officer.Id, approver.Id, collector.Id, manager.Id, today, loanPrefix);
            await SeedApplicationPipelineAsync(db, officer.Id, approver.Id, products, branchId);
            await SeedReconciliationExceptionsAsync(db, manager.Id, today);
            Console.WriteLine($"[Seeder] Test portfolio already has {existingCount} loans. Support data verified.");
            return;
        }

        for (var i = 1; i <= targetCount; i++)
        {
            var loanAccountNo = $"{loanPrefix}{i:0000}";
            if (await db.Loans.AnyAsync(l => l.LoanAccountNo == loanAccountNo))
            {
                continue;
            }

            var product = products[(i - 1) % products.Count];
            var tenor = product.GetAllowedTenors().FirstOrDefault();
            if (tenor <= 0) tenor = i % 3 == 0 ? 18 : i % 2 == 0 ? 12 : 6;

            var approvedAmount = Math.Min(
                product.MaxAmount,
                Math.Max(product.MinAmount, 8_000_000m + (i % 12) * 3_000_000m));

            var status = (i % 4) switch
            {
                0 => LoanStatus.PendingDisbursement,
                1 => LoanStatus.Active,
                2 => LoanStatus.Overdue,
                _ => LoanStatus.Closed
            };

            var customerPhone = $"{phonePrefix}{i:00000}";
            var customer = await db.Customers.FirstOrDefaultAsync(c => c.Phone == customerPhone);
            if (customer is null)
            {
                customer = new Customer
                {
                    FullName = $"Test Customer {i:000}",
                    Phone = customerPhone,
                    IdNumber = $"0792026{i:00000}",
                    Address = $"Test address {i:000}, Ho Chi Minh City",
                    DateOfBirth = new DateTime(1980 + i % 20, (i % 12) + 1, (i % 27) + 1),
                    CreatedByUserId = officer.Id,
                    AssignedToUserId = officer.Id,
                    BranchId = branchId,
                    EkycStatus = i % 9 == 0 ? "ManualReview" : "Verified",
                    EkycVerifiedAt = i % 9 == 0 ? null : DateTime.UtcNow.AddDays(-i),
                    CreditScore = 520 + i % 320,
                    CreditStatus = i % 11 == 0 ? "Review" : "Pass",
                    BankAccountNumber = $"9704{i:000000000}",
                    BankCode = i % 2 == 0 ? "VCB" : "TCB",
                    MonthlyIncome = 12_000_000m + (i % 10) * 2_000_000m,
                    IndustryCode = i % 2 == 0 ? "G47" : "C10",
                    RegionCode = i % 3 == 0 ? "HN" : "HCM",
                    AmlRiskLevel = i % 17 == 0 ? "High" : i % 5 == 0 ? "Medium" : "Low",
                    PepStatus = i % 23 == 0 ? "Domestic" : "None",
                    SourceOfFunds = i % 4 == 0 ? "Business income" : "Salary",
                    Occupation = i % 3 == 0 ? "Business owner" : "Employee",
                    Nationality = "VN"
                };

                db.Customers.Add(customer);
                await db.SaveChangesAsync();
            }

            var applicationRemarks = $"TST approved application {i:000}";
            var application = await db.LoanApplications.FirstOrDefaultAsync(a => a.Remarks == applicationRemarks);
            if (application is null)
            {
                application = new LoanApplication
                {
                    CustomerId = customer.Id,
                    ProductId = product.Id,
                    RequestedAmount = approvedAmount,
                    RequestedTenorMonths = tenor,
                    Purpose = i % 3 == 0 ? "Business working capital" : "Personal loan test case",
                    Remarks = applicationRemarks,
                    Status = ApplicationStatus.Approved,
                    CreatedByUserId = officer.Id,
                    SubmittedAt = DateTime.UtcNow.AddDays(-60 - i),
                    RequiredApprovalLevel = approvedAmount >= 40_000_000m ? 2 : 1,
                    CurrentApprovalLevel = approvedAmount >= 40_000_000m ? 2 : 1
                };

                db.LoanApplications.Add(application);
                await db.SaveChangesAsync();
            }

            if (!await db.ApplicationDecisions.AnyAsync(d => d.ApplicationId == application.Id && d.Decision == "Approved"))
            {
                db.ApplicationDecisions.Add(new ApplicationDecision
                {
                    ApplicationId = application.Id,
                    Decision = "Approved",
                    DecidedByUserId = approver.Id,
                    DecidedAt = DateTime.UtcNow.AddDays(-55 - i)
                });
            }

            var disbursementDate = status == LoanStatus.PendingDisbursement
                ? (DateOnly?)null
                : today.AddDays(-(20 + i % 90));

            var loan = new Loan
            {
                LoanAccountNo = loanAccountNo,
                ApplicationId = application.Id,
                CustomerId = customer.Id,
                ProductId = product.Id,
                ApprovedAmount = approvedAmount,
                TenorMonths = tenor,
                MonthlyInterestRate = product.MonthlyInterestRate,
                ProcessingFee = product.ProcessingFee,
                DisbursementDate = disbursementDate,
                MaturityDate = disbursementDate?.AddMonths(tenor),
                Status = status,
                OutstandingAmount = status == LoanStatus.Closed ? 0 : approvedAmount,
                VirtualAccountNo = $"VA2026TST{i:000000}",
                CurrentClassification = status == LoanStatus.Overdue ? LoanClassification.Group3_Substandard : LoanClassification.Group1_Standard,
                ProvisionAmount = status == LoanStatus.Overdue ? approvedAmount * 0.2m : 0,
                Ifrs9Stage = status == LoanStatus.Overdue ? 3 : 1,
                Ifrs9EclAmount = status == LoanStatus.Overdue ? approvedAmount * 0.15m : approvedAmount * 0.01m,
                PurposeCode = i % 3 == 0 ? "BUSINESS" : "CONSUMER",
                IndustryCode = customer.IndustryCode,
                ProductType = product.ProductType,
                InterestMethod = product.InterestMethod,
                GracePeriodMonths = product.GracePeriodMonths,
                PromotionalRate = product.PromotionalRate,
                PromotionalMonths = product.PromotionalMonths,
                EarlySettlementFeeRate = product.EarlySettlementFeeRate,
                LateFeeRate = product.LateFeeRate == 0 ? 0.12m : product.LateFeeRate,
                LateFeeGraceDays = product.LateFeeGraceDays,
                BranchId = branchId
            };

            db.Loans.Add(loan);
            await db.SaveChangesAsync();

            if (status != LoanStatus.PendingDisbursement && disbursementDate.HasValue)
            {
                await AddDisbursementAsync(db, loan.Id, approvedAmount, disbursementDate.Value, officer.Id);
                await SeedSchedulesAndPaymentsAsync(db, loan, status, disbursementDate.Value, officer.Id, today);
            }

            if (i % 3 == 0)
            {
                await SeedCollateralAsync(db, customer.Id, loan.Id, approvedAmount, i);
            }

            if (status == LoanStatus.Overdue)
            {
                await SeedCollectionDataAsync(db, loan.Id, collector.Id, manager.Id, today, i);
            }

            if (i % 5 == 0 && status != LoanStatus.PendingDisbursement)
            {
                SeedApprovalRequest(db, loan.Id, officer.Id, approver.Id, i, today);
            }

            if (i <= 30)
            {
                db.Notifications.Add(new Notification
                {
                    UserId = i % 2 == 0 ? officer.Id : manager.Id,
                    LoanId = loan.Id,
                    CustomerId = customer.Id,
                    Type = i % 3 == 0 ? NotificationType.OverdueReminder : NotificationType.System,
                    Title = $"Test notification {i:000}",
                    Message = $"Seeded test event for loan {loan.LoanAccountNo}.",
                    DeeplinkUrl = i % 2 == 0 ? "/loans" : "/reports",
                    IsRead = i % 4 == 0,
                    ReadAt = i % 4 == 0 ? DateTime.UtcNow.AddDays(-1) : null,
                    CreatedAt = DateTime.UtcNow.AddHours(-i)
                });
            }

            db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = officer.Id,
                ActionCode = AuditActions.LoanCreated,
                EntityType = "Loan",
                EntityId = loan.Id,
                NewValuesJson = JsonSerializer.Serialize(new { loan.LoanAccountNo, testSeed = true })
            });

            await db.SaveChangesAsync();
        }

        await EnsureTestPortfolioSupportDataAsync(db, officer.Id, approver.Id, collector.Id, manager.Id, today, loanPrefix);
        await SeedApplicationPipelineAsync(db, officer.Id, approver.Id, products, branchId);
        await SeedReconciliationExceptionsAsync(db, manager.Id, today);

        Console.WriteLine($"[Seeder] Test portfolio seeded to {targetCount} loans.");
    }

    private static async Task SeedSchedulesAndPaymentsAsync(
        LoanFlowDbContext db,
        Loan loan,
        string loanStatus,
        DateOnly disbursementDate,
        int officerUserId,
        DateOnly today)
    {
        var principalPerInstallment = Math.Round(loan.ApprovedAmount / loan.TenorMonths, 2);
        var totalScheduledPrincipal = 0m;
        var interestPerInstallment = Math.Round(loan.ApprovedAmount * loan.MonthlyInterestRate, 2);
        var paidInstallments = loanStatus switch
        {
            LoanStatus.Closed => loan.TenorMonths,
            LoanStatus.Active => Math.Min(3, Math.Max(1, loan.TenorMonths - 1)),
            _ => 0
        };
        var overdueInstallments = loanStatus == LoanStatus.Overdue
            ? Math.Min(3, loan.TenorMonths)
            : 0;

        var schedules = new List<RepaymentSchedule>();
        for (var month = 1; month <= loan.TenorMonths; month++)
        {
            var principal = month == loan.TenorMonths
                ? loan.ApprovedAmount - totalScheduledPrincipal
                : principalPerInstallment;
            totalScheduledPrincipal += principal;

            var isPaid = month <= paidInstallments;
            var isOverdue = month <= overdueInstallments;
            schedules.Add(new RepaymentSchedule
            {
                LoanId = loan.Id,
                InstallmentNo = month,
                DueDate = disbursementDate.AddMonths(month),
                PrincipalDue = principal,
                InterestDue = interestPerInstallment,
                PrincipalPaid = isPaid ? principal : 0,
                InterestPaid = isPaid ? interestPerInstallment : 0,
                RemainingDueAmount = isPaid ? 0 : principal + interestPerInstallment,
                Status = isPaid ? InstallmentStatus.Paid : isOverdue ? InstallmentStatus.Overdue : InstallmentStatus.Unpaid
            });
        }

        db.RepaymentSchedules.AddRange(schedules);
        await db.SaveChangesAsync();

        var outstandingPrincipal = loan.ApprovedAmount;
        foreach (var schedule in schedules.Where(s => s.Status == InstallmentStatus.Paid))
        {
            var payment = new Payment
            {
                LoanId = loan.Id,
                PaymentDate = schedule.DueDate.AddDays(-1),
                Amount = schedule.PrincipalDue + schedule.InterestDue,
                PaymentMethod = schedule.InstallmentNo % 2 == 0 ? PaymentMethod.BankTransfer : PaymentMethod.Cash,
                ExternalReferenceId = schedule.InstallmentNo % 2 == 0 ? $"TST-PAY-{loan.Id}-{schedule.InstallmentNo}" : null,
                Remarks = $"Seed payment for installment {schedule.InstallmentNo}",
                CreatedByUserId = officerUserId
            };

            db.Payments.Add(payment);
            await db.SaveChangesAsync();

            var transaction = new LoanTransaction
            {
                LoanId = loan.Id,
                PaymentId = payment.Id,
                TransactionType = "Payment",
                Amount = payment.Amount,
                ReferenceNo = $"PAY-TST-{payment.Id}"
            };
            db.LoanTransactions.Add(transaction);
            await db.SaveChangesAsync();

            db.PaymentAllocations.AddRange(
                new PaymentAllocation
                {
                    PaymentId = payment.Id,
                    LoanTransactionId = transaction.Id,
                    ScheduleId = schedule.Id,
                    AllocationType = "Principal",
                    Amount = schedule.PrincipalDue
                },
                new PaymentAllocation
                {
                    PaymentId = payment.Id,
                    LoanTransactionId = transaction.Id,
                    ScheduleId = schedule.Id,
                    AllocationType = "Interest",
                    Amount = schedule.InterestDue
                });

            outstandingPrincipal -= schedule.PrincipalDue;
        }

        await SeedOverdueArtifactsAsync(db, loan, schedules, today, checkExisting: false);

        loan.OutstandingAmount = loanStatus == LoanStatus.Closed ? 0 : Math.Max(0, outstandingPrincipal);
        await db.SaveChangesAsync();
    }

    private static async Task SeedOverdueArtifactsAsync(
        LoanFlowDbContext db,
        Loan loan,
        IReadOnlyCollection<RepaymentSchedule> schedules,
        DateOnly today,
        bool checkExisting)
    {
        var overdueSchedules = schedules
            .Where(s => s.Status == InstallmentStatus.Overdue)
            .OrderBy(s => s.DueDate)
            .ToList();

        if (overdueSchedules.Count == 0) return;

        var existingSnapshotDates = checkExisting
            ? await db.OverdueDailySnapshots
                .Where(s => s.LoanId == loan.Id)
                .Select(s => s.SnapshotDate)
                .ToListAsync()
            : [];

        var totalOverdueAmount = overdueSchedules.Sum(s => s.RemainingDueAmount);
        var totalLateFeeAmount = 0m;
        var maxDpd = 1;

        foreach (var schedule in overdueSchedules)
        {
            var dpd = Math.Max(1, today.DayNumber - schedule.DueDate.DayNumber);
            var lateFeeAmount = Math.Round(schedule.RemainingDueAmount * loan.LateFeeRate * dpd / 360m, 2);
            totalLateFeeAmount += lateFeeAmount;
            maxDpd = Math.Max(maxDpd, dpd);

            var hasLateFee = checkExisting &&
                await db.LateFees.AnyAsync(f => f.LoanId == loan.Id && f.ScheduleId == schedule.Id);

            if (!hasLateFee)
            {
                db.LateFees.Add(new LateFee
                {
                    LoanId = loan.Id,
                    ScheduleId = schedule.Id,
                    OverdueAmount = schedule.RemainingDueAmount,
                    DaysOverdue = dpd,
                    FeeRate = loan.LateFeeRate,
                    FeeAmount = lateFeeAmount,
                    Status = LateFeeStatus.Pending,
                    CalculatedAt = DateTime.UtcNow.AddDays(-1)
                });
            }
        }

        for (var d = 0; d < 10; d++)
        {
            var snapshotDate = today.AddDays(-d);
            if (existingSnapshotDates.Contains(snapshotDate))
            {
                continue;
            }

            var snapshotDpd = Math.Max(1, maxDpd - d);
            db.OverdueDailySnapshots.Add(new OverdueDailySnapshot
            {
                LoanId = loan.Id,
                SnapshotDate = snapshotDate,
                Dpd = snapshotDpd,
                Bucket = DpdBucket.Calculate(snapshotDpd),
                OverdueAmount = totalOverdueAmount,
                OutstandingAmount = loan.OutstandingAmount,
                LateFeeAccrued = totalLateFeeAmount,
                OverdueInstallments = overdueSchedules.Count
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureTestPortfolioSupportDataAsync(
        LoanFlowDbContext db,
        int officerUserId,
        int approverUserId,
        int collectorUserId,
        int managerUserId,
        DateOnly today,
        string loanPrefix)
    {
        var loans = await db.Loans
            .Where(l => l.LoanAccountNo.StartsWith(loanPrefix))
            .OrderBy(l => l.Id)
            .ToListAsync();

        foreach (var loan in loans)
        {
            var schedules = await db.RepaymentSchedules
                .Where(s => s.LoanId == loan.Id)
                .OrderBy(s => s.InstallmentNo)
                .ToListAsync();

            if (schedules.Count == 0 && loan.Status != LoanStatus.PendingDisbursement && loan.DisbursementDate.HasValue)
            {
                await SeedSchedulesAndPaymentsAsync(db, loan, loan.Status, loan.DisbursementDate.Value, officerUserId, today);
                schedules = await db.RepaymentSchedules
                    .Where(s => s.LoanId == loan.Id)
                    .OrderBy(s => s.InstallmentNo)
                    .ToListAsync();
            }

            if (loan.Status != LoanStatus.Overdue)
            {
                await EnsureTestLoanAncillaryDataAsync(db, loan, officerUserId, approverUserId, managerUserId, today, loanPrefix);
                continue;
            }

            await SeedOverdueArtifactsAsync(db, loan, schedules, today, checkExisting: true);

            var hasCollectionAssignment = await db.CollectorAssignments
                .AnyAsync(a => a.LoanId == loan.Id && a.Status == CollectorAssignmentStatus.Active);
            if (!hasCollectionAssignment)
            {
                await SeedCollectionDataAsync(db, loan.Id, collectorUserId, managerUserId, today, loan.Id);
            }

            await EnsureTestLoanAncillaryDataAsync(db, loan, officerUserId, approverUserId, managerUserId, today, loanPrefix);
        }
    }

    private static async Task EnsureTestLoanAncillaryDataAsync(
        LoanFlowDbContext db,
        Loan loan,
        int officerUserId,
        int approverUserId,
        int managerUserId,
        DateOnly today,
        string loanPrefix)
    {
        if (!TryGetTestLoanIndex(loan.LoanAccountNo, loanPrefix, out var index))
        {
            return;
        }

        if (index % 3 == 0 && !await db.LoanCollaterals.AnyAsync(c => c.LoanId == loan.Id))
        {
            await SeedCollateralAsync(db, loan.CustomerId, loan.Id, loan.ApprovedAmount, index);
        }

        if (index % 5 == 0 && loan.Status != LoanStatus.PendingDisbursement)
        {
            var loanIdMarker = $"\"LoanId\":{loan.Id}";
            var hasApprovalRequest = await db.PendingApprovalRequests
                .AnyAsync(r => r.Payload.Contains(loanIdMarker));

            if (!hasApprovalRequest)
            {
                SeedApprovalRequest(db, loan.Id, officerUserId, approverUserId, index, today);
                await db.SaveChangesAsync();
            }
        }

        if (index <= 30)
        {
            var title = $"Test notification {index:000}";
            if (!await db.Notifications.AnyAsync(n => n.Title == title))
            {
                db.Notifications.Add(new Notification
                {
                    UserId = index % 2 == 0 ? officerUserId : managerUserId,
                    LoanId = loan.Id,
                    CustomerId = loan.CustomerId,
                    Type = index % 3 == 0 ? NotificationType.OverdueReminder : NotificationType.System,
                    Title = title,
                    Message = $"Seeded test event for loan {loan.LoanAccountNo}.",
                    DeeplinkUrl = index % 2 == 0 ? "/loans" : "/reports",
                    IsRead = index % 4 == 0,
                    ReadAt = index % 4 == 0 ? DateTime.UtcNow.AddDays(-1) : null,
                    CreatedAt = DateTime.UtcNow.AddHours(-index)
                });

                await db.SaveChangesAsync();
            }
        }
    }

    private static bool TryGetTestLoanIndex(string loanAccountNo, string loanPrefix, out int index)
    {
        index = 0;
        return loanAccountNo.StartsWith(loanPrefix, StringComparison.Ordinal) &&
            int.TryParse(loanAccountNo[loanPrefix.Length..], out index);
    }

    private static async Task SeedCollateralAsync(LoanFlowDbContext db, int customerId, int loanId, decimal approvedAmount, int index)
    {
        var collateralType = index % 2 == 0 ? CollateralType.Vehicle : CollateralType.RealEstate;
        var collateral = new Collateral
        {
            CustomerId = customerId,
            CollateralType = collateralType,
            Description = collateralType == CollateralType.Vehicle
                ? $"Test vehicle collateral {index:000}"
                : $"Test real estate collateral {index:000}",
            RegistrationNo = collateralType == CollateralType.Vehicle ? $"51A-TST{index:000}" : $"LAND-TST-{index:000}",
            AppraisedValue = approvedAmount * (collateralType == CollateralType.Vehicle ? 1.3m : 2.0m),
            AppraisalDate = DateTime.UtcNow.AddDays(-20 - index),
            AppraisedBy = "Seed Appraisal Team",
            Status = CollateralStatus.Active,
            Address = collateralType == CollateralType.RealEstate ? $"Collateral address {index:000}" : null
        };

        db.Collaterals.Add(collateral);
        await db.SaveChangesAsync();

        db.LoanCollaterals.Add(new LoanCollateral
        {
            LoanId = loanId,
            CollateralId = collateral.Id,
            AllocatedValue = approvedAmount * 1.2m,
            Status = "Active"
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedCollectionDataAsync(
        LoanFlowDbContext db,
        int loanId,
        int collectorUserId,
        int managerUserId,
        DateOnly today,
        int index)
    {
        db.CollectorAssignments.Add(new CollectorAssignment
        {
            LoanId = loanId,
            CollectorUserId = collectorUserId,
            AssignedAt = DateTime.UtcNow.AddDays(-index % 20),
            Status = CollectorAssignmentStatus.Active,
            Priority = index % 3 == 0 ? CollectionPriority.High : CollectionPriority.Medium
        });

        db.CollectionActions.AddRange(
            new CollectionAction
            {
                LoanId = loanId,
                CollectorUserId = collectorUserId,
                ActionType = CollectionActionType.Call,
                ActionDate = today.AddDays(-3),
                NextFollowUpDate = today.AddDays(2),
                Outcome = CollectionOutcome.PromiseToPay,
                PromiseDate = today.AddDays(5),
                PromiseAmount = 1_500_000m + index * 10_000m,
                PromiseStatus = index % 4 == 0 ? PromiseStatus.Broken : PromiseStatus.Pending,
                Notes = "Seed collection promise."
            },
            new CollectionAction
            {
                LoanId = loanId,
                CollectorUserId = collectorUserId,
                ActionType = CollectionActionType.SMS,
                ActionDate = today.AddDays(-1),
                NextFollowUpDate = today.AddDays(3),
                Outcome = index % 2 == 0 ? CollectionOutcome.Contacted : CollectionOutcome.NoAnswer,
                Notes = "Seed follow-up SMS."
            });

        db.CollectionNotes.Add(new CollectionNote
        {
            LoanId = loanId,
            NoteDate = today.AddDays(-2),
            NoteText = "Seed overdue collection note.",
            CreatedByUserId = collectorUserId
        });

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = managerUserId,
            ActionCode = AuditActions.CollectorAssigned,
            EntityType = "Loan",
            EntityId = loanId,
            NewValuesJson = JsonSerializer.Serialize(new { collectorUserId, testSeed = true })
        });

        await db.SaveChangesAsync();
    }

    private static void SeedApprovalRequest(
        LoanFlowDbContext db,
        int loanId,
        int makerUserId,
        int checkerUserId,
        int index,
        DateOnly today)
    {
        var actionType = (index % 4) switch
        {
            0 => "LoanRestructure",
            1 => "LoanReschedule",
            2 => "LoanEarlySettle",
            _ => "ManualAdjustment"
        };

        var payload = actionType switch
        {
            "LoanRestructure" => JsonSerializer.Serialize(new
            {
                LoanId = loanId,
                Request = new
                {
                    NewTenorMonths = 18,
                    NewMonthlyInterestRate = 0.012m,
                    RestructureStartDate = today,
                    ReasonCode = "CustomerHardship",
                    Note = "Seed restructure approval."
                }
            }),
            "LoanReschedule" => JsonSerializer.Serialize(new
            {
                LoanId = loanId,
                Request = new
                {
                    NewPaymentDayOfMonth = 25,
                    EffectiveFrom = today.AddDays(7),
                    ReasonCode = "PayrollDateChanged",
                    Note = "Seed reschedule approval."
                }
            }),
            "LoanEarlySettle" => JsonSerializer.Serialize(new
            {
                LoanId = loanId,
                Request = new
                {
                    SettlementDate = today.AddDays(3),
                    WaiveFee = false
                }
            }),
            _ => JsonSerializer.Serialize(new
            {
                LoanId = loanId,
                AdjustmentType = "Interest",
                Amount = -100_000m,
                ReasonCode = "CustomerHardship",
                Note = "Seed manual adjustment approval."
            })
        };

        db.PendingApprovalRequests.Add(new PendingApprovalRequest
        {
            ActionType = actionType,
            Payload = payload,
            RequestedByUserId = makerUserId,
            ApprovedByUserId = index % 15 == 0 ? checkerUserId : null,
            Status = index % 15 == 0 ? "Approved" : "Pending",
            CreatedAt = DateTime.UtcNow.AddHours(-index),
            DecidedAt = index % 15 == 0 ? DateTime.UtcNow.AddHours(-index + 1) : null
        });
    }

    private static async Task SeedApplicationPipelineAsync(
        LoanFlowDbContext db,
        int officerUserId,
        int approverUserId,
        List<LoanProduct> products,
        int? branchId)
    {
        if (await db.LoanApplications.AnyAsync(a => a.Remarks == "TST pipeline application 001")) return;

        var customers = await db.Customers
            .Where(c => c.Phone.StartsWith("09877"))
            .OrderBy(c => c.Id)
            .Take(25)
            .ToListAsync();

        if (customers.Count == 0 || products.Count == 0) return;

        for (var i = 1; i <= customers.Count; i++)
        {
            var status = (i % 3) switch
            {
                0 => ApplicationStatus.Draft,
                1 => ApplicationStatus.Submitted,
                _ => ApplicationStatus.Rejected
            };
            var product = products[(i - 1) % products.Count];
            var application = new LoanApplication
            {
                CustomerId = customers[i - 1].Id,
                ProductId = product.Id,
                RequestedAmount = Math.Min(product.MaxAmount, Math.Max(product.MinAmount, 15_000_000m + i * 500_000m)),
                RequestedTenorMonths = product.GetAllowedTenors().FirstOrDefault() is var tenor && tenor > 0 ? tenor : 12,
                Purpose = "Pipeline UI test",
                Remarks = $"TST pipeline application {i:000}",
                Status = status,
                CreatedByUserId = officerUserId,
                SubmittedAt = status == ApplicationStatus.Draft ? null : DateTime.UtcNow.AddDays(-i),
                CurrentApprovalLevel = status == ApplicationStatus.Submitted ? 1 : null,
                RequiredApprovalLevel = status == ApplicationStatus.Submitted ? 2 : null
            };

            db.LoanApplications.Add(application);
            await db.SaveChangesAsync();

            if (status == ApplicationStatus.Rejected)
            {
                db.ApplicationDecisions.Add(new ApplicationDecision
                {
                    ApplicationId = application.Id,
                    Decision = "Rejected",
                    DecisionReason = "Seed rejected application for UI testing.",
                    DecidedByUserId = approverUserId,
                    DecidedAt = DateTime.UtcNow.AddDays(-i).AddHours(2)
                });
            }

            if (branchId.HasValue)
            {
                customers[i - 1].BranchId = branchId;
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedReconciliationExceptionsAsync(LoanFlowDbContext db, int resolvedByUserId, DateOnly today)
    {
        if (await db.ReconciliationExceptions.AnyAsync(e => e.Description != null && e.Description.StartsWith("TST reconciliation"))) return;

        var loans = await db.Loans
            .Where(l => l.LoanAccountNo.StartsWith("LN-TST-2026-"))
            .OrderBy(l => l.Id)
            .Take(12)
            .ToListAsync();

        for (var i = 0; i < loans.Count; i++)
        {
            var expected = loans[i].OutstandingAmount;
            var actual = expected + (i % 2 == 0 ? 50_000m : -25_000m);
            db.ReconciliationExceptions.Add(new ReconciliationException
            {
                BusinessDate = today.AddDays(-i % 5),
                ExceptionType = i % 2 == 0 ? ReconciliationExceptionType.PaymentMismatch : ReconciliationExceptionType.OutstandingMismatch,
                LoanId = loans[i].Id,
                ExpectedAmount = expected,
                ActualAmount = actual,
                Difference = actual - expected,
                Description = $"TST reconciliation exception {i + 1:000}",
                Status = i % 4 == 0 ? ReconciliationStatus.Resolved : ReconciliationStatus.Open,
                ResolvedByUserId = i % 4 == 0 ? resolvedByUserId : null,
                ResolvedAt = i % 4 == 0 ? DateTime.UtcNow.AddDays(-1) : null,
                Resolution = i % 4 == 0 ? "Seed resolved exception." : null
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedPermissionsAsync(LoanFlowDbContext db)
    {
        if (await db.Permissions.AnyAsync()) return;

        var permissions = new[]
        {
            new Permission { Code = "LOAN_VIEW", Name = "View Loans", Module = "Loans" },
            new Permission { Code = "LOAN_CREATE", Name = "Create Loans", Module = "Loans" },
            new Permission { Code = "LOAN_APPROVE", Name = "Approve Loans", Module = "Loans" },
            new Permission { Code = "CUSTOMER_VIEW", Name = "View Customers", Module = "Customers" },
            new Permission { Code = "CUSTOMER_CREATE", Name = "Create Customers", Module = "Customers" },
            new Permission { Code = "REPORT_VIEW", Name = "View Reports", Module = "Reports" },
            new Permission { Code = "USER_MANAGE", Name = "Manage Users", Module = "Admin" }
        };

        db.Permissions.AddRange(permissions);

        var rolePermissions = new List<RolePermission>();

        // Admin gets everything
        foreach (var p in permissions)
        {
            rolePermissions.Add(new RolePermission { RoleCode = "Admin", PermissionCode = p.Code });
        }

        // Loan Officer
        rolePermissions.Add(new RolePermission { RoleCode = "LoanOfficer", PermissionCode = "LOAN_VIEW" });
        rolePermissions.Add(new RolePermission { RoleCode = "LoanOfficer", PermissionCode = "LOAN_CREATE" });
        rolePermissions.Add(new RolePermission { RoleCode = "LoanOfficer", PermissionCode = "CUSTOMER_VIEW" });
        rolePermissions.Add(new RolePermission { RoleCode = "LoanOfficer", PermissionCode = "CUSTOMER_CREATE" });

        // Approver / Manager
        rolePermissions.Add(new RolePermission { RoleCode = "Approver", PermissionCode = "LOAN_VIEW" });
        rolePermissions.Add(new RolePermission { RoleCode = "Approver", PermissionCode = "LOAN_APPROVE" });
        rolePermissions.Add(new RolePermission { RoleCode = "Approver", PermissionCode = "CUSTOMER_VIEW" });
        
        rolePermissions.Add(new RolePermission { RoleCode = "Manager", PermissionCode = "LOAN_VIEW" });
        rolePermissions.Add(new RolePermission { RoleCode = "Manager", PermissionCode = "LOAN_APPROVE" });
        rolePermissions.Add(new RolePermission { RoleCode = "Manager", PermissionCode = "REPORT_VIEW" });

        db.RolePermissions.AddRange(rolePermissions);
        await db.SaveChangesAsync();
    }

    private static async Task SeedGLAccountsAsync(LoanFlowDbContext db)
    {
        if (await db.GLAccounts.AnyAsync()) return; // Already seeded — idempotent guard

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
            new GLAccount { AccountCode = "4003", AccountName = "Processing Fee Income", AccountType = GLAccountType.Income },
            new GLAccount { AccountCode = "4004", AccountName = "Early Settle Fee Income", AccountType = GLAccountType.Income },
            new GLAccount { AccountCode = "4005", AccountName = "Recovery Income", AccountType = GLAccountType.Income },
            
            new GLAccount { AccountCode = "5001", AccountName = "Provision Expense", AccountType = GLAccountType.Expense },
            new GLAccount { AccountCode = "5002", AccountName = "Write-Off Expense", AccountType = GLAccountType.Expense },
        };
        db.GLAccounts.AddRange(accounts);
        await db.SaveChangesAsync();
    }

    private static async Task SeedGLAccountMappingsAsync(LoanFlowDbContext db)
    {
        if (await db.GLAccountMappings.AnyAsync()) return;

        var mappings = new List<GLAccountMapping>
        {
            new GLAccountMapping { TransactionType = "Disbursement", DebitAccountCode = "1301", CreditAccountCode = "1001", Description = "Loan Disbursement" },
            new GLAccountMapping { TransactionType = "ProcessingFee", DebitAccountCode = "1001", CreditAccountCode = "4003", Description = "Processing Fee Deduction" },
            new GLAccountMapping { TransactionType = "Repayment_Principal", DebitAccountCode = "1001", CreditAccountCode = "1301", Description = "Principal Repayment" },
            new GLAccountMapping { TransactionType = "Repayment_Interest", DebitAccountCode = "1001", CreditAccountCode = "1302", Description = "Interest Repayment" },
            new GLAccountMapping { TransactionType = "Repayment_LateFee", DebitAccountCode = "1001", CreditAccountCode = "1303", Description = "Late Fee Repayment" },
            new GLAccountMapping { TransactionType = "Overpayment_Suspense", DebitAccountCode = "1001", CreditAccountCode = "2001", Description = "Overpayment to Suspense" },
            new GLAccountMapping { TransactionType = "Repayment_EarlySettleFee", DebitAccountCode = "1001", CreditAccountCode = "4004", Description = "Early Settlement Fee Income" },
            new GLAccountMapping { TransactionType = "LateFee_Accrual", DebitAccountCode = "1303", CreditAccountCode = "4002", Description = "Late Fee Accrual" },
            new GLAccountMapping { TransactionType = "LateFee_Waiver", DebitAccountCode = "4002", CreditAccountCode = "1303", Description = "Late Fee Waiver" }
        };

        db.GLAccountMappings.AddRange(mappings);
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
        var webhookSystem = new User
        {
            Username = "system-webhook",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(ResolvePassword("SEED_WEBHOOK_SYSTEM_PASSWORD")),
            FullName = "Webhook System Actor",
            Role = Roles.Admin,
            Status = "Inactive"
        };

        db.Users.AddRange(admin, officer, approver, collector, manager, webhookSystem);
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
