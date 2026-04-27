using FluentAssertions;
using LoanFlow.Application.Services;
using Xunit;
using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace LoanFlow.Application.Tests.Services;

public class ClassificationJobServiceTests
{
    private readonly DbContextOptions<LoanFlowDbContext> _dbOptions;

    public ClassificationJobServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<LoanFlowDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task RunDailyClassificationAsync_ShouldApplyCrossClassification_AndCalculateProvisions()
    {
        // Arrange
        var dbContext = new LoanFlowDbContext(_dbOptions);
        var mockAccountingService = new Mock<IAccountingService>();
        mockAccountingService.Setup(x => x.PostJournalAsync(It.IsAny<JournalEntryDto>()))
            .ReturnsAsync(LoanFlow.Application.Common.Result.Success(new JournalEntry()));
        var mockLogger = new Mock<ILogger<ClassificationJobService>>();
        var service = new ClassificationJobService(dbContext, mockAccountingService.Object, mockLogger.Object);

        var businessDate = new DateOnly(2026, 4, 27);
        var todaySys = businessDate.ToDateTime(TimeOnly.MinValue);

        // Customer with 2 loans
        var customerId = 1;
        var loan1 = new Loan
        {
            Id = 1,
            LoanAccountNo = "LN001",
            CustomerId = customerId,
            OutstandingAmount = 100000000,
            Status = LoanStatus.Active,
            CurrentClassification = LoanClassification.Group1_Standard,
            RepaymentSchedules = new List<RepaymentSchedule>
            {
                new RepaymentSchedule { DueDate = DateOnly.FromDateTime(todaySys.AddDays(-15)), Status = InstallmentStatus.Unpaid } // DPD 15 -> Group 2
            },
            LoanCollaterals = new List<LoanCollateral>()
        };

        var loan2 = new Loan
        {
            Id = 2,
            LoanAccountNo = "LN002",
            CustomerId = customerId,
            OutstandingAmount = 50000000,
            Status = LoanStatus.Active,
            CurrentClassification = LoanClassification.Group1_Standard,
            QualitativeClassification = LoanClassification.Group3_Substandard, // Group 3 due to qualitative
            RepaymentSchedules = new List<RepaymentSchedule>
            {
                new RepaymentSchedule { DueDate = DateOnly.FromDateTime(todaySys.AddDays(-5)), Status = InstallmentStatus.Unpaid } // DPD 5 -> Group 1
            },
            LoanCollaterals = new List<LoanCollateral>()
        };

        dbContext.Loans.AddRange(loan1, loan2);
        await dbContext.SaveChangesAsync();

        // Act
        await service.RunDailyClassificationAsync(businessDate);

        // Assert
        var updatedLoan1 = await dbContext.Loans.FindAsync(1);
        var updatedLoan2 = await dbContext.Loans.FindAsync(2);

        // Both loans should have Group 3 (Cross-classification from loan2's Qualitative rule)
        updatedLoan1.CurrentClassification.Should().Be(LoanClassification.Group3_Substandard);
        updatedLoan2.CurrentClassification.Should().Be(LoanClassification.Group3_Substandard);

        // Provision for Group 3 = 20%
        // loan1: 100M * 0.2 + 0.75% = 20,000,000 + 750,000 = 20,750,000
        updatedLoan1.ProvisionAmount.Should().Be(20750000);
        // loan2: 50M * 0.2 + 0.75% = 10,000,000 + 375,000 = 10,375,000
        updatedLoan2.ProvisionAmount.Should().Be(10375000);
    }
}
