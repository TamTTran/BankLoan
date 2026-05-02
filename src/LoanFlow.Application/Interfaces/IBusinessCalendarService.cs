namespace LoanFlow.Application.Services;

public interface IBusinessClock
{
    DateTime UtcNow { get; }
    DateTime LocalNow { get; }
    DateOnly TodayBusinessDate();
}

public interface IBusinessCalendarService
{
    DateOnly GetCurrentBusinessDate();
    DateOnly ResolveBusinessDate(DateTime utcTimestamp);
    bool IsBusinessDay(DateOnly date);
    DateOnly NextBusinessDay(DateOnly date);
    DateOnly PreviousBusinessDay(DateOnly date);
    bool IsAfterCutoff(DateTime localTime);
    (DateTime StartUtc, DateTime EndUtc) GetBusinessDayUtcRange(DateOnly businessDate);
}
