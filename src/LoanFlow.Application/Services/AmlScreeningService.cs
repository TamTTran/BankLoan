using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public interface ISanctionListProvider
{
    Task<(bool IsHit, string? Details)> CheckNameAsync(string fullName, string? idNumber);
}

public class MockSanctionListProvider : ISanctionListProvider
{
    private readonly List<string> _mockBlacklist = new() { "BIN LADEN", "PABLO ESCOBAR", "EL CHAPO" };

    public Task<(bool IsHit, string? Details)> CheckNameAsync(string fullName, string? idNumber)
    {
        var upperName = fullName.ToUpper();
        if (_mockBlacklist.Any(name => upperName.Contains(name)))
        {
            return Task.FromResult((true, (string?)"Matched OFAC Specially Designated Nationals (SDN) List"));
        }
        return Task.FromResult((false, (string?)null));
    }
}

public interface IAmlScreeningService
{
    Task<AmlRiskAssessment> ScreenCustomerAsync(int customerId, int currentUserId);
}

public class AmlScreeningService : IAmlScreeningService
{
    private readonly LoanFlowDbContext _db;
    private readonly ISanctionListProvider _sanctionProvider;

    public AmlScreeningService(LoanFlowDbContext db, ISanctionListProvider sanctionProvider)
    {
        _db = db;
        _sanctionProvider = sanctionProvider;
    }

    public async Task<AmlRiskAssessment> ScreenCustomerAsync(int customerId, int currentUserId)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
        if (customer == null) throw new ArgumentException("Customer not found");

        var sanctionCheck = await _sanctionProvider.CheckNameAsync(customer.FullName, customer.IdNumber);

        // Determine Risk Level
        string riskLevel = "Low";
        
        if (sanctionCheck.IsHit || customer.PepStatus == "Foreign")
        {
            riskLevel = "High";
        }
        else if (customer.PepStatus == "Domestic" || customer.Nationality != "VN" || 
                 customer.Occupation?.Contains("Crypto") == true || customer.Occupation?.Contains("Casino") == true)
        {
            riskLevel = "Medium";
        }

        customer.AmlRiskLevel = riskLevel;

        var assessment = new AmlRiskAssessment
        {
            CustomerId = customerId,
            RiskLevel = riskLevel,
            PepStatus = customer.PepStatus,
            HasSanctionHit = sanctionCheck.IsHit,
            SanctionDetails = sanctionCheck.Details,
            LastScreeningDate = DateTime.UtcNow,
            NextReviewDate = riskLevel == "High" ? DateTime.UtcNow.AddMonths(6) : DateTime.UtcNow.AddYears(1),
            AssessedByUserId = currentUserId,
            Remarks = sanctionCheck.IsHit ? "Sanction Hit Found" : "Screening Passed"
        };

        _db.AmlRiskAssessments.Add(assessment);
        await _db.SaveChangesAsync();

        return assessment;
    }
}
