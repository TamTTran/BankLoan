using System.Text.Json;
using FluentAssertions;
using LoanFlow.Application.DTOs.ApprovalFlow;
using LoanFlow.Application.Services;
using LoanFlow.Application.Tests.Support;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LoanFlow.Application.Tests.Services;

public class ApprovalFlowServiceTests
{
    [Fact]
    public async Task ApproveRequestAsync_Should_Execute_WriteOff_Action()
    {
        var databaseName = Guid.NewGuid().ToString();
        var clock = new TestBusinessClock(
            utcNow: new DateTime(2026, 4, 19, 1, 0, 0, DateTimeKind.Utc),
            localNow: new DateTime(2026, 4, 19, 8, 0, 0, DateTimeKind.Unspecified),
            businessDate: new DateOnly(2026, 4, 19));

        var services = new ServiceCollection();
        services.AddDbContext<LoanFlowDbContext>(options => options
            .UseInMemoryDatabase(databaseName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));
        services.AddSingleton<IBusinessClock>(clock);
        services.AddScoped<IApprovalFlowService, ApprovalFlowService>();
        services.AddScoped<ILoanLifecycleService, LoanLifecycleService>();
        services.AddScoped<ILoanAdjustmentService, LoanAdjustmentService>();

        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LoanFlowDbContext>();
            db.Users.AddRange(
                new User { Id = 1, FullName = "Maker", Username = "maker", PasswordHash = "hash", Role = Roles.Manager },
                new User { Id = 2, FullName = "Checker", Username = "checker", PasswordHash = "hash", Role = Roles.Admin });
            db.Loans.Add(new Loan
            {
                Id = 10,
                LoanAccountNo = "LN-WRITEOFF-001",
                OutstandingAmount = 1_500m,
                Status = LoanStatus.Overdue
            });
            db.RepaymentSchedules.Add(new RepaymentSchedule
            {
                Id = 1,
                LoanId = 10,
                InstallmentNo = 1,
                DueDate = new DateOnly(2026, 3, 10),
                PrincipalDue = 1_000m,
                InterestDue = 500m,
                RemainingDueAmount = 1_500m,
                Status = InstallmentStatus.Unpaid
            });
            await db.SaveChangesAsync();
        }

        int approvalId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var approvalService = scope.ServiceProvider.GetRequiredService<IApprovalFlowService>();
            var submit = await approvalService.SubmitRequestAsync(new SubmitApprovalRequest
            {
                ActionType = "LoanWriteOff",
                Payload = JsonSerializer.Serialize(new WriteOffApprovalPayload
                {
                    LoanId = 10,
                    Request = new WriteOffRequest
                    {
                        ReasonCode = "Uncollectible",
                        WriteOffDate = new DateOnly(2026, 4, 19),
                        Note = "test"
                    }
                })
            }, 1);

            submit.IsSuccess.Should().BeTrue();
            approvalId = submit.Value.Id;
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var approvalService = scope.ServiceProvider.GetRequiredService<IApprovalFlowService>();
            var approve = await approvalService.ApproveRequestAsync(approvalId, 2);

            approve.IsSuccess.Should().BeTrue();
            approve.Value.Status.Should().Be("Approved");
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LoanFlowDbContext>();
            var loan = await db.Loans.FindAsync(10);
            var writeOff = await db.LoanWriteOffs.SingleOrDefaultAsync(w => w.LoanId == 10);

            loan!.Status.Should().Be(LoanStatus.WrittenOff);
            loan.OutstandingAmount.Should().Be(0);
            writeOff.Should().NotBeNull();
            writeOff!.ApprovedByUserId.Should().Be(2);
        }
    }
}
