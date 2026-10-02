namespace HrServiceDesk.Domain.Sla;

/// <summary>Business-time arithmetic. Implementations are pure: time always comes from the caller.</summary>
public interface IBusinessTimeCalculator
{
    /// <summary>The instant reached after <paramref name="minutes"/> of business time from <paramref name="start"/>.</summary>
    DateTimeOffset AddBusinessMinutes(DateTimeOffset start, int minutes);

    /// <summary>Business minutes between two instants (0 when <paramref name="to"/> is not after <paramref name="from"/>).</summary>
    int BusinessMinutesBetween(DateTimeOffset from, DateTimeOffset to);
}

/// <summary>
/// Computes deadlines over a <see cref="BusinessCalendar"/>. Working intervals are local wall-clock times,
/// converted to UTC per day, so daylight saving changes are handled: an interval spanning a DST switch
/// lasts what it really lasts. Holidays and days without intervals are skipped.
/// </summary>
public sealed class BusinessTimeCalculator : IBusinessTimeCalculator
{
    /// <summary>Safety bound: a deadline further than ten years away means a broken calendar.</summary>
    private const int MaxDaysScanned = 3660;

    private readonly TimeZoneInfo _zone;
    private readonly ILookup<DayOfWeek, WorkingInterval> _intervals;
    private readonly HashSet<DateOnly> _holidays;

    public BusinessTimeCalculator(BusinessCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        _zone = TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZoneId);
        _intervals = calendar.WorkingHours.OrderBy(i => i.Start).ToLookup(i => i.Day);
        _holidays = [.. calendar.Holidays.Select(h => h.Date)];
        if (calendar.WorkingHours.Count == 0)
            throw new InvalidOperationException("The calendar has no working hours.");
    }

    public DateTimeOffset AddBusinessMinutes(DateTimeOffset start, int minutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minutes);
        var remaining = TimeSpan.FromMinutes(minutes);
        var cursor = start.ToUniversalTime();
        if (remaining == TimeSpan.Zero)
            return cursor;

        foreach (var (from, to) in IntervalsFrom(cursor))
        {
            var begin = from > cursor ? from : cursor;
            var available = to - begin;
            if (available >= remaining)
                return begin + remaining;
            remaining -= available;
        }

        throw new InvalidOperationException("No business time found within ten years: check the calendar.");
    }

    public int BusinessMinutesBetween(DateTimeOffset from, DateTimeOffset to)
    {
        var start = from.ToUniversalTime();
        var end = to.ToUniversalTime();
        if (end <= start)
            return 0;

        var total = TimeSpan.Zero;
        foreach (var (intervalStart, intervalEnd) in IntervalsFrom(start))
        {
            if (intervalStart >= end)
                break;
            var begin = intervalStart > start ? intervalStart : start;
            var finish = intervalEnd < end ? intervalEnd : end;
            if (finish > begin)
                total += finish - begin;
        }

        return (int)Math.Floor(total.TotalMinutes);
    }

    /// <summary>Working intervals in UTC, in order, starting with the local day containing <paramref name="fromUtc"/>.</summary>
    private IEnumerable<(DateTimeOffset From, DateTimeOffset To)> IntervalsFrom(DateTimeOffset fromUtc)
    {
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(fromUtc, _zone).DateTime);
        for (var i = 0; i < MaxDaysScanned; i++, day = day.AddDays(1))
        {
            if (_holidays.Contains(day))
                continue;
            foreach (var interval in _intervals[day.DayOfWeek])
            {
                var startUtc = ToUtc(day, interval.Start);
                var endUtc = ToUtc(day, interval.End);
                if (endUtc > fromUtc && endUtc > startUtc)
                    yield return (startUtc, endUtc);
            }
        }
    }

    /// <summary>Local wall time to UTC. Times skipped by a DST jump move forward; repeated times use the first occurrence.</summary>
    private DateTimeOffset ToUtc(DateOnly day, TimeOnly time)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        while (_zone.IsInvalidTime(local))
            local = local.AddMinutes(1);
        var offset = _zone.IsAmbiguousTime(local) ? _zone.GetAmbiguousTimeOffsets(local).Max() : _zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}
