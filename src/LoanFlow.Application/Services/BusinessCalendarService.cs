using Microsoft.Extensions.Options;

namespace LoanFlow.Application.Services;

public class BusinessDateOptions
{
    public string TimeZoneId { get; set; } = "SE Asia Standard Time";
    public int CutoffHour { get; set; } = 17;
    public int CutoffMinute { get; set; } = 0;
    public List<string> Holidays { get; set; } = [];
}

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

public class BusinessClock : IBusinessClock
{
    private readonly IBusinessCalendarService _calendar;
    private readonly TimeZoneInfo _timeZone;

    public BusinessClock(IOptions<BusinessDateOptions> options, IBusinessCalendarService calendar)
    {
        _calendar = calendar;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);
    }

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(UtcNow, _timeZone);

    public DateOnly TodayBusinessDate() => _calendar.GetCurrentBusinessDate();
}

public class BusinessCalendarService : IBusinessCalendarService
{
    private readonly TimeZoneInfo _timeZone;
    private readonly TimeOnly _cutoffTime;
    private readonly HashSet<DateOnly> _holidays;

    public BusinessCalendarService(IOptions<BusinessDateOptions> options)
    {
        var settings = options.Value;
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        _cutoffTime = new TimeOnly(settings.CutoffHour, settings.CutoffMinute);
        _holidays = settings.Holidays
            .Select(DateOnly.Parse)
            .ToHashSet();
    }

    public DateOnly GetCurrentBusinessDate() => ResolveBusinessDate(DateTime.UtcNow);

    public DateOnly ResolveBusinessDate(DateTime utcTimestamp)
    {
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcTimestamp, _timeZone);
        var candidate = DateOnly.FromDateTime(localTime);
        if (IsAfterCutoff(localTime))
        {
            candidate = candidate.AddDays(1);
        }

        return IsBusinessDay(candidate) ? candidate : NextBusinessDay(candidate);
    }

    public bool IsBusinessDay(DateOnly date)
    {
        var dayOfWeek = date.DayOfWeek;
        return dayOfWeek != DayOfWeek.Saturday
            && dayOfWeek != DayOfWeek.Sunday
            && !_holidays.Contains(date);
    }

    public DateOnly NextBusinessDay(DateOnly date)
    {
        var next = date;
        do
        {
            next = next.AddDays(1);
        } while (!IsBusinessDay(next));

        return next;
    }

    public DateOnly PreviousBusinessDay(DateOnly date)
    {
        var previous = date;
        do
        {
            previous = previous.AddDays(-1);
        } while (!IsBusinessDay(previous));

        return previous;
    }

    public bool IsAfterCutoff(DateTime localTime) => TimeOnly.FromDateTime(localTime) >= _cutoffTime;

    public (DateTime StartUtc, DateTime EndUtc) GetBusinessDayUtcRange(DateOnly businessDate)
    {
        var startLocal = businessDate.AddDays(-1).ToDateTime(_cutoffTime, DateTimeKind.Unspecified);
        var endLocal = businessDate.ToDateTime(_cutoffTime, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, _timeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, _timeZone);
        return (startUtc, endUtc);
    }
}
