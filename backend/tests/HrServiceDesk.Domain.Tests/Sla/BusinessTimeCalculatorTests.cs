using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Sla;

namespace HrServiceDesk.Domain.Tests.Sla;

public class BusinessTimeCalculatorTests
{
    private static readonly TimeOnly T0800 = new(8, 0), T1200 = new(12, 0), T1300 = new(13, 0), T1700 = new(17, 0);

    /// <summary>Tunisia: Monday–Friday 08:00–12:00 and 13:00–17:00, UTC+1 all year; 20 March is a holiday.</summary>
    private static BusinessCalendar Tunisia() => BusinessCalendar.Create(
        "Tunisia",
        "Africa/Tunis",
        Weekdays().SelectMany(d => new[] { new WorkingInterval(d, T0800, T1200), new WorkingInterval(d, T1300, T1700) }),
        [new Holiday(new DateOnly(2026, 3, 20), "Independence Day")]);

    private static IEnumerable<DayOfWeek> Weekdays() =>
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    private static readonly BusinessTimeCalculator Calc = new(Tunisia());

    /// <summary>Local Tunis time (UTC+1).</summary>
    private static DateTimeOffset Tn(int month, int day, int hour, int minute = 0) => new(2026, month, day, hour, minute, 0, TimeSpan.FromHours(1));

    // 2026-03-16 is a Monday; 2026-03-20 (Friday) is a holiday in this calendar.

    [Theory]
    [InlineData(9, 0, 60, 16, 10, 0)]   // within the morning
    [InlineData(11, 30, 60, 16, 13, 30)] // across the lunch break
    [InlineData(6, 0, 30, 16, 8, 30)]   // before opening
    [InlineData(12, 15, 15, 16, 13, 15)] // starting during lunch
    [InlineData(8, 0, 240, 16, 12, 0)]  // ends exactly at a boundary
    [InlineData(18, 0, 30, 17, 8, 30)]  // after closing: next morning
    [InlineData(8, 0, 1440, 18, 17, 0)] // three full days
    public void Adds_business_minutes_on_a_working_week(int h, int m, int minutes, int expectedDay, int eh, int em) =>
        Calc.AddBusinessMinutes(Tn(3, 16, h, m), minutes).Should().Be(Tn(3, expectedDay, eh, em));

    [Fact]
    public void Skips_the_weekend() =>
        Calc.AddBusinessMinutes(Tn(3, 13, 16), 120).Should().Be(Tn(3, 16, 9));

    [Fact]
    public void Skips_a_public_holiday_and_the_weekend_after_it() =>
        // Thursday 16:30 + 1h: 30 min on Thursday, Friday 20 March is a holiday, then Saturday/Sunday.
        Calc.AddBusinessMinutes(Tn(3, 19, 16, 30), 60).Should().Be(Tn(3, 23, 8, 30));

    [Fact]
    public void Zero_minutes_returns_the_start() =>
        Calc.AddBusinessMinutes(Tn(3, 14, 23), 0).Should().Be(Tn(3, 14, 23));

    [Fact]
    public void Accepts_any_offset_and_returns_utc()
    {
        var result = Calc.AddBusinessMinutes(new DateTimeOffset(2026, 3, 16, 7, 0, 0, TimeSpan.Zero), 30);

        result.Offset.Should().Be(TimeSpan.Zero);
        result.Should().Be(Tn(3, 16, 8, 30));
    }

    [Theory]
    [InlineData(3, 13, 16, 0, 3, 16, 9, 0, 120)] // Friday 16:00 → Monday 09:00
    [InlineData(3, 19, 16, 30, 3, 23, 8, 30, 60)] // across the holiday
    [InlineData(3, 16, 11, 0, 3, 16, 14, 0, 120)] // lunch excluded
    [InlineData(3, 14, 10, 0, 3, 15, 18, 0, 0)] // weekend only
    [InlineData(3, 16, 10, 0, 3, 16, 9, 0, 0)] // reversed
    public void Counts_business_minutes_between_instants(int m1, int d1, int h1, int mi1, int m2, int d2, int h2, int mi2, int expected) =>
        Calc.BusinessMinutesBetween(Tn(m1, d1, h1, mi1), Tn(m2, d2, h2, mi2)).Should().Be(expected);

    [Fact]
    public void Adding_then_measuring_gives_back_the_same_minutes()
    {
        var random = new Random(20260316);
        for (var i = 0; i < 500; i++)
        {
            var start = Tn(1, 1, 0).AddMinutes(random.Next(0, 60 * 24 * 365));
            var minutes = random.Next(0, 5000);

            var due = Calc.AddBusinessMinutes(start, minutes);

            Calc.BusinessMinutesBetween(start, due).Should().Be(minutes, $"start {start:o}, {minutes} min");
            due.Should().BeOnOrAfter(start);
        }
    }

    [Fact]
    public void Daylight_saving_switches_change_the_real_length_of_an_interval()
    {
        // Paris, Sunday nights 00:00–06:00 only. 29 March 2026 loses an hour, 25 October 2026 gains one.
        var calendar = BusinessCalendar.Create("Night", "Europe/Paris", [new WorkingInterval(DayOfWeek.Sunday, new TimeOnly(0, 0), new TimeOnly(6, 0))], []);
        var calc = new BusinessTimeCalculator(calendar);

        calc.BusinessMinutesBetween(new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 30, 0, 0, 0, TimeSpan.Zero))
            .Should().Be(300);
        calc.BusinessMinutesBetween(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero))
            .Should().Be(420);
        // 04:00 local after the spring switch is 02:00 UTC: three business hours after midnight (22:00 UTC the day before).
        calc.AddBusinessMinutes(new DateTimeOffset(2026, 3, 28, 23, 0, 0, TimeSpan.Zero), 180)
            .Should().Be(new DateTimeOffset(2026, 3, 29, 2, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Calendars_are_validated()
    {
        var noHours = () => BusinessCalendar.Create("Empty", "Africa/Tunis", [], []);
        noHours.Should().Throw<DomainException>().Which.Code.Should().Be("calendar.no_working_hours");

        var overlap = () => BusinessCalendar.Create("Overlap", "Africa/Tunis",
            [new WorkingInterval(DayOfWeek.Monday, T0800, T1300), new WorkingInterval(DayOfWeek.Monday, T1200, T1700)], []);
        overlap.Should().Throw<DomainException>().Which.Code.Should().Be("calendar.overlapping_intervals");

        var badZone = () => BusinessCalendar.Create("Mars", "Mars/Olympus", [new WorkingInterval(DayOfWeek.Monday, T0800, T1200)], []);
        badZone.Should().Throw<DomainException>().Which.Code.Should().Be("calendar.invalid_time_zone");

        var calendar = Tunisia();
        var duplicate = () => calendar.AddHoliday(new DateOnly(2026, 3, 20), "Again");
        duplicate.Should().Throw<DomainException>().Which.Code.Should().Be("calendar.duplicate_holiday");
        calendar.RemoveHoliday(new DateOnly(2026, 3, 20));
        new BusinessTimeCalculator(calendar).AddBusinessMinutes(Tn(3, 19, 16, 30), 60).Should().Be(Tn(3, 20, 8, 30));
    }
}
