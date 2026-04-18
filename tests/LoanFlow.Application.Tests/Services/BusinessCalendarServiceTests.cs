using FluentAssertions;
using LoanFlow.Application.Services;
using Microsoft.Extensions.Options;

namespace LoanFlow.Application.Tests.Services;

public class BusinessCalendarServiceTests
{
    private static BusinessCalendarService CreateService(params string[] holidays)
    {
        return new BusinessCalendarService(Options.Create(new BusinessDateOptions
        {
            TimeZoneId = "SE Asia Standard Time",
            CutoffHour = 17,
            CutoffMinute = 0,
            Holidays = holidays.ToList()
        }));
    }

    [Fact]
    public void ResolveBusinessDate_Should_KeepSameDay_BeforeCutoff()
    {
        var service = CreateService();
        var utc = new DateTime(2026, 4, 18, 8, 30, 0, DateTimeKind.Utc); // 15:30 local

        var businessDate = service.ResolveBusinessDate(utc);

        businessDate.Should().Be(new DateOnly(2026, 4, 18));
    }

    [Fact]
    public void ResolveBusinessDate_Should_MoveToNextBusinessDay_AfterCutoff()
    {
        var service = CreateService();
        var utc = new DateTime(2026, 4, 20, 10, 30, 0, DateTimeKind.Utc); // 17:30 local Monday

        var businessDate = service.ResolveBusinessDate(utc);

        businessDate.Should().Be(new DateOnly(2026, 4, 21));
    }

    [Fact]
    public void ResolveBusinessDate_Should_SkipWeekendAndHoliday()
    {
        var service = CreateService("2026-04-20");
        var utc = new DateTime(2026, 4, 17, 11, 30, 0, DateTimeKind.Utc); // 18:30 local Friday

        var businessDate = service.ResolveBusinessDate(utc);

        businessDate.Should().Be(new DateOnly(2026, 4, 21));
    }

    [Fact]
    public void GetBusinessDayUtcRange_Should_MapVietnamMidnightToUtcWindow()
    {
        var service = CreateService();

        var (startUtc, endUtc) = service.GetBusinessDayUtcRange(new DateOnly(2026, 4, 18));

        startUtc.Should().Be(new DateTime(2026, 4, 17, 10, 0, 0, DateTimeKind.Utc));
        endUtc.Should().Be(new DateTime(2026, 4, 18, 10, 0, 0, DateTimeKind.Utc));
    }
}
