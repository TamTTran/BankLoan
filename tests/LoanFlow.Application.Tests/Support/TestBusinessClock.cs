using LoanFlow.Application.Services;

namespace LoanFlow.Application.Tests.Support;

internal sealed class TestBusinessClock : IBusinessClock
{
    private readonly DateTime _utcNow;
    private readonly DateTime _localNow;
    private readonly DateOnly _businessDate;

    public TestBusinessClock(DateTime utcNow, DateTime localNow, DateOnly businessDate)
    {
        _utcNow = utcNow;
        _localNow = localNow;
        _businessDate = businessDate;
    }

    public DateTime UtcNow => _utcNow;

    public DateTime LocalNow => _localNow;

    public DateOnly TodayBusinessDate() => _businessDate;
}
