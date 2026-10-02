using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Sla;

/// <summary>A working-time interval on a weekday, in the calendar's local time (e.g. Monday 08:00–12:00).</summary>
public sealed record WorkingInterval(DayOfWeek Day, TimeOnly Start, TimeOnly End);

/// <summary>A non-working day (whole day, local date).</summary>
public sealed record Holiday(DateOnly Date, string Name);

/// <summary>
/// When business time runs for a tenant: weekly working intervals (lunch breaks are gaps between
/// intervals), the IANA time zone they are expressed in, and public holidays.
/// </summary>
public sealed class BusinessCalendar : Entity, ITenantOwned, IAuditable
{
    public const int NameMaxLength = 100;

    private readonly List<WorkingInterval> _workingHours = [];
    private readonly List<Holiday> _holidays = [];

    private BusinessCalendar() { }

    public Guid TenantId { get; set; }

    public string Name { get; private set; } = string.Empty;

    public string TimeZoneId { get; private set; } = "UTC";

    public IReadOnlyCollection<WorkingInterval> WorkingHours => _workingHours.AsReadOnly();

    public IReadOnlyCollection<Holiday> Holidays => _holidays.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public static BusinessCalendar Create(string name, string timeZoneId, IEnumerable<WorkingInterval> workingHours, IEnumerable<Holiday> holidays)
    {
        var calendar = new BusinessCalendar();
        calendar.Update(name, timeZoneId, workingHours);
        foreach (var holiday in holidays ?? [])
            calendar.AddHoliday(holiday.Date, holiday.Name);
        return calendar;
    }

    public void Update(string name, string timeZoneId, IEnumerable<WorkingInterval> workingHours)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length is 0 or > NameMaxLength)
            throw new DomainException("calendar.invalid_name", $"Calendar name must be 1 to {NameMaxLength} characters.");
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId ?? string.Empty, out _))
            throw new DomainException("calendar.invalid_time_zone", $"Unknown time zone '{timeZoneId}'.");

        var intervals = (workingHours ?? []).OrderBy(i => i.Day).ThenBy(i => i.Start).ToList();
        if (intervals.Count == 0)
            throw new DomainException("calendar.no_working_hours", "A calendar needs at least one working interval.");
        if (intervals.Any(i => i.End <= i.Start))
            throw new DomainException("calendar.invalid_interval", "Each working interval must end after it starts.");
        foreach (var day in intervals.GroupBy(i => i.Day))
        {
            var ordered = day.ToList();
            for (var k = 1; k < ordered.Count; k++)
            {
                if (ordered[k].Start < ordered[k - 1].End)
                    throw new DomainException("calendar.overlapping_intervals", $"Working intervals overlap on {day.Key}.");
            }
        }

        Name = name;
        TimeZoneId = timeZoneId!;
        _workingHours.Clear();
        _workingHours.AddRange(intervals);
    }

    public void AddHoliday(DateOnly date, string name)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length is 0 or > 100)
            throw new DomainException("calendar.invalid_holiday", "Holiday names must be 1 to 100 characters.");
        if (_holidays.Any(h => h.Date == date))
            throw new DomainException("calendar.duplicate_holiday", $"{date:yyyy-MM-dd} is already a holiday.");
        _holidays.Add(new Holiday(date, name));
    }

    public void RemoveHoliday(DateOnly date) => _holidays.RemoveAll(h => h.Date == date);

    public bool IsHoliday(DateOnly date) => _holidays.Any(h => h.Date == date);
}
