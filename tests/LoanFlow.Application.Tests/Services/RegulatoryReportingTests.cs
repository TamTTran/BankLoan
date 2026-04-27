using LoanFlow.Application.Services;
using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;

namespace LoanFlow.Application.Tests.Services;

public class RegulatoryReportingTests : IDisposable
{
    private readonly LoanFlowDbContext _db;
    private readonly CapitalAdequacyService _carService;
    private readonly LiquidityReportService _liquidityService;

    public RegulatoryReportingTests()
    {
        var options = new DbContextOptionsBuilder<LoanFlowDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new LoanFlowDbContext(options);

        _carService = new CapitalAdequacyService(_db);
        _liquidityService = new LiquidityReportService(_db);
    }

    [Fact]
    public async Task CalculateCarAsync_WithValidLoans_CalculatesCorrectly()
    {
        // Arrange
        var customer = new Customer { FullName = "Test", Phone = "123", IdNumber = "123", Address = "ABC" };
        var productSme = new LoanProduct { ProductName = "SME", ProductType = "SME", Description = "SME" };
        var productPersonal = new LoanProduct { ProductName = "Personal", ProductType = "PersonalUnsecured", Description = "Personal" };

        _db.Customers.Add(customer);
        _db.LoanProducts.AddRange(productSme, productPersonal);
        await _db.SaveChangesAsync();

        _db.Loans.Add(new Loan
        {
            LoanAccountNo = "LN1",
            Customer = customer,
            Product = productSme,
            ProductType = "SME",
            OutstandingAmount = 100_000_000m,
            Status = LoanStatus.Active
        });

        _db.Loans.Add(new Loan
        {
            LoanAccountNo = "LN2",
            Customer = customer,
            Product = productPersonal,
            ProductType = "PersonalUnsecured",
            OutstandingAmount = 50_000_000m,
            Status = LoanStatus.Active
        });

        await _db.SaveChangesAsync();

        // Act
        var result = await _carService.CalculateCarAsync(2026, 4);

        // Assert
        result.Should().NotBeNull();
        // RWA: SME = 100M * 100% = 100M
        // Personal = 50M * 75% = 37.5M
        // Total RWA (loans) = 137.5M
        // Mock Cash = 100B * 0% = 0
        // Total RWA = 137,500,000
        result.TotalRiskWeightedAssets.Should().Be(137_500_000m);
        result.Tier1Capital.Should().Be(50_000_000_000m);
        result.CarRatio.Should().BeGreaterThan(8.0m);
        result.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public async Task CalculateLiquidityAsync_ReturnsValidLcrAndNsfr()
    {
        // Arrange
        var today = new DateOnly(2026, 4, 1);
        var loan = new Loan { LoanAccountNo = "LN1", Status = LoanStatus.Active, TenorMonths = 24, OutstandingAmount = 500_000_000m };
        _db.Loans.Add(loan);

        _db.RepaymentSchedules.Add(new RepaymentSchedule
        {
            Loan = loan,
            DueDate = today.AddDays(15),
            PrincipalDue = 10_000_000m,
            InterestDue = 2_000_000m,
            Status = InstallmentStatus.Unpaid
        });

        await _db.SaveChangesAsync();

        // Act
        var result = await _liquidityService.GenerateReportAsync(2026, 4);

        // Assert
        result.Should().NotBeNull();
        // LCR
        result.HighQualityLiquidAssets.Should().Be(150_000_000_000m);
        // Net Outflows = 100B (mock deposits) - 12M (inflows) = 99,988,000,000
        result.TotalNetCashOutflows30Days.Should().Be(99_988_000_000m);
        result.IsLcrCompliant.Should().BeTrue();

        // NSFR
        result.AvailableStableFunding.Should().Be(600_000_000_000m);
        // RSF = 500M (loan > 1 year) + 100B (mock) = 100,500,000,000
        result.RequiredStableFunding.Should().Be(100_500_000_000m);
        result.IsNsfrCompliant.Should().BeTrue();
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }
}
