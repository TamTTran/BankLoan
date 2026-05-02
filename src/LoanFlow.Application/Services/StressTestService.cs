using LoanFlow.Domain.Constants;
using LoanFlow.Domain.Entities;
using LoanFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LoanFlow.Application.Services;

public class StressTestService : IStressTestService
{
    private readonly LoanFlowDbContext _db;
    private readonly IRiskModelService _riskModelService;

    public StressTestService(LoanFlowDbContext db, IRiskModelService riskModelService)
    {
        _db = db;
        _riskModelService = riskModelService;
    }

    public async Task<List<StressTestResultDto>> RunStressTestsAsync()
    {
        var basePortfolio = await _riskModelService.GetPortfolioExpectedLossAsync();

        var scenarios = new List<StressTestScenario>
        {
            new StressTestScenario 
            { 
                ScenarioName = "Interest Rate Shock +2%", 
                InterestRateShockParams = 0.02m,
                PdMultiplier = 1.20m, // PD increases by 20%
                LgdMultiplier = 1.05m // LGD increases by 5% due to slightly lower collateral value
            },
            new StressTestScenario 
            { 
                ScenarioName = "Interest Rate Shock +5%", 
                InterestRateShockParams = 0.05m,
                PdMultiplier = 1.50m, // PD increases by 50%
                LgdMultiplier = 1.15m
            },
            new StressTestScenario 
            { 
                ScenarioName = "GDP Contraction -3%", 
                GdpContractionParams = -0.03m,
                PdMultiplier = 2.00m, // PD doubles
                LgdMultiplier = 1.30m // Collateral values drop significantly
            }
        };

        var results = new List<StressTestResultDto>();

        foreach (var scenario in scenarios)
        {
            decimal stressedEl = 0;
            decimal stressedNplVolume = 0;

            foreach (var loan in basePortfolio.LoanDetails)
            {
                var stressedPd = Math.Min(loan.ProbabilityOfDefault * scenario.PdMultiplier, 1.0m);
                var stressedLgd = Math.Min(loan.LossGivenDefault * scenario.LgdMultiplier, 1.0m);
                var el = loan.ExposureAtDefault * stressedPd * stressedLgd;
                
                stressedEl += el;

                // Estimate stressed NPL: if stressed PD > 50%, consider it likely to migrate to NPL
                if (stressedPd >= 0.50m)
                {
                    stressedNplVolume += loan.ExposureAtDefault;
                }
            }

            results.Add(new StressTestResultDto
            {
                ScenarioName = scenario.ScenarioName,
                BaseExpectedLoss = basePortfolio.TotalExpectedLoss,
                StressedExpectedLoss = stressedEl,
                ImpactAmount = stressedEl - basePortfolio.TotalExpectedLoss,
                ImpactPercentage = basePortfolio.TotalExpectedLoss > 0 
                    ? Math.Round((stressedEl - basePortfolio.TotalExpectedLoss) / basePortfolio.TotalExpectedLoss * 100, 2) 
                    : 0,
                StressedPortfolioNplRatio = basePortfolio.TotalExposure > 0 
                    ? Math.Round(stressedNplVolume / basePortfolio.TotalExposure * 100, 2) 
                    : 0
            });
        }

        return results;
    }
}
