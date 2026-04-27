using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class ExpectedLossDto
{
    public int LoanId { get; set; }
    public decimal ExposureAtDefault { get; set; }
    public decimal ProbabilityOfDefault { get; set; } // 0 to 1
    public decimal LossGivenDefault { get; set; }     // 0 to 1
    public decimal ExpectedLoss { get; set; }
    public int CurrentDpd { get; set; }
}

public class PortfolioExpectedLossDto
{
    public DateOnly AsOfDate { get; set; }
    public decimal TotalExposure { get; set; }
    public decimal TotalExpectedLoss { get; set; }
    public decimal PortfolioExpectedLossRate { get; set; }
    public List<ExpectedLossDto> LoanDetails { get; set; } = [];
}

public interface IRiskModelService
{
    Task<ExpectedLossDto> CalculateExpectedLossAsync(int loanId);
    Task<PortfolioExpectedLossDto> GetPortfolioExpectedLossAsync();
}

public class RiskModelService : IRiskModelService
{
    private readonly LoanFlowDbContext _db;

    public RiskModelService(LoanFlowDbContext db)
    {
        _db = db;
    }

    public async Task<ExpectedLossDto> CalculateExpectedLossAsync(int loanId)
    {
        var loan = await _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.LoanCollaterals)
            .ThenInclude(lc => lc.Collateral)
            .Include(l => l.RepaymentSchedules)
            .FirstOrDefaultAsync(l => l.Id == loanId);

        if (loan == null) throw new ArgumentException("Loan not found");

        return CalculateElInternal(loan);
    }

    public async Task<PortfolioExpectedLossDto> GetPortfolioExpectedLossAsync()
    {
        var activeLoans = await _db.Loans
            .Include(l => l.Customer)
            .Include(l => l.LoanCollaterals)
            .ThenInclude(lc => lc.Collateral)
            .Include(l => l.RepaymentSchedules)
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Restructured)
            .ToListAsync();

        var details = activeLoans.Select(CalculateElInternal).ToList();

        var totalEad = details.Sum(d => d.ExposureAtDefault);
        var totalEl = details.Sum(d => d.ExpectedLoss);

        return new PortfolioExpectedLossDto
        {
            AsOfDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TotalExposure = totalEad,
            TotalExpectedLoss = totalEl,
            PortfolioExpectedLossRate = totalEad > 0 ? Math.Round(totalEl / totalEad, 4) : 0,
            LoanDetails = details
        };
    }

    private ExpectedLossDto CalculateElInternal(Loan loan)
    {
        // 1. Calculate EAD (Exposure At Default)
        // EAD = Outstanding Principal + Outstanding Interest
        var ead = loan.OutstandingAmount;

        // 2. Calculate LGD (Loss Given Default)
        // LGD = 1 - Recovery Rate
        decimal lgd = 1.0m; // Unsecured loan -> 100% loss
        
        var totalCollateralValue = loan.LoanCollaterals
            .Where(lc => lc.Status == "Active")
            .Sum(lc => lc.AllocatedValue);

        if (totalCollateralValue > 0)
        {
            // Simple recovery model: Collateral Value / EAD
            // Haircut is already applied in AllocatedValue conceptually or we can apply it here
            var recoveryRate = ead > 0 ? totalCollateralValue / ead : 1.0m;
            if (recoveryRate > 1.0m) recoveryRate = 1.0m;
            
            lgd = 1.0m - recoveryRate;
        }

        // 3. Calculate PD (Probability of Default)
        // Simple mock PD model based on Credit Score and DPD
        decimal pd = 0.05m; // Base PD 5%
        
        // Adjust for Credit Score
        int score = loan.Customer.CreditScore ?? 0;
        if (score >= 800) pd = 0.001m; // 0.1%
        else if (score >= 700) pd = 0.01m; // 1%
        else if (score >= 600) pd = 0.05m; // 5%
        else if (score >= 500) pd = 0.15m; // 15%
        else if (score > 0) pd = 0.30m; // 30%
        
        // Adjust for DPD (Days Past Due)
        var unpaidSchedules = loan.RepaymentSchedules
            .Where(s => s.Status != InstallmentStatus.Paid && s.DueDate < DateOnly.FromDateTime(DateTime.UtcNow))
            .ToList();
            
        int currentDpd = unpaidSchedules.Any() 
            ? (DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - unpaidSchedules.Min(s => s.DueDate).DayNumber)
            : 0;

        if (currentDpd > 90) pd = 1.0m; // Certain default
        else if (currentDpd > 30) pd = Math.Max(pd, 0.50m);
        else if (currentDpd > 0) pd = Math.Max(pd, 0.20m);

        if (loan.Status == LoanStatus.Restructured) pd = Math.Max(pd, 0.40m); // Restructured loans carry higher risk

        // 4. Expected Loss
        decimal el = ead * pd * lgd;

        return new ExpectedLossDto
        {
            LoanId = loan.Id,
            ExposureAtDefault = ead,
            ProbabilityOfDefault = Math.Round(pd, 4),
            LossGivenDefault = Math.Round(lgd, 4),
            ExpectedLoss = Math.Round(el, 2),
            CurrentDpd = currentDpd
        };
    }
}
