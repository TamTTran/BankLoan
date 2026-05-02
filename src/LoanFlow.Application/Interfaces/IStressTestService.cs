namespace LoanFlow.Application.Services;

public interface IStressTestService
{
    Task<List<StressTestResultDto>> RunStressTestsAsync();
}
