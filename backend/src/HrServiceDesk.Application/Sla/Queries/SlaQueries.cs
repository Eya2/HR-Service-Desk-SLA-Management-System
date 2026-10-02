using System.Globalization;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Sla;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Sla.Queries;

internal static class SlaMapping
{
    public static readonly Error CalendarNotFound = Error.NotFound("calendar.not_found", "No business calendar is configured.");
    public static readonly Error PolicyNotFound = Error.NotFound("sla.not_found", "SLA policy not found.");

    public static CalendarDto ToDto(BusinessCalendar c) => new(
        c.Id,
        c.Name,
        c.TimeZoneId,
        c.WorkingHours.OrderBy(i => i.Day == DayOfWeek.Sunday ? 7 : (int)i.Day).ThenBy(i => i.Start)
            .Select(i => new WorkingIntervalDto(i.Day.ToString(), i.Start.ToString("HH:mm", CultureInfo.InvariantCulture), i.End.ToString("HH:mm", CultureInfo.InvariantCulture))).ToList(),
        c.Holidays.OrderBy(h => h.Date).Select(h => new HolidayDto(h.Date, h.Name)).ToList());

    public static SlaPolicyDto ToDto(SlaPolicy p, IEnumerable<string> requestTypes) => new(
        p.Id,
        p.Name,
        p.IsDefault,
        p.AtRiskThresholdPercent,
        p.Targets.OrderBy(t => t.Priority).Select(t => new SlaTargetDto(t.Priority.ToString(), t.FirstResponseMinutes, t.ResolutionMinutes)).ToList(),
        p.PauseStatuses.Select(s => s.ToString()).ToList(),
        requestTypes.Order().ToList());
}

/// <summary>The organisation's business calendar.</summary>
public sealed record GetCalendarQuery : IRequest<Result<CalendarDto>>;

internal sealed class GetCalendarHandler(IAppDbContext db) : IRequestHandler<GetCalendarQuery, Result<CalendarDto>>
{
    public async Task<Result<CalendarDto>> Handle(GetCalendarQuery request, CancellationToken cancellationToken)
    {
        var calendar = await db.BusinessCalendars.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return calendar is null ? SlaMapping.CalendarNotFound : SlaMapping.ToDto(calendar);
    }
}

/// <summary>What a deadline would be: <paramref name="Minutes"/> business minutes after <paramref name="Start"/>.</summary>
public sealed record PreviewDeadlineQuery(DateTimeOffset Start, int Minutes) : IRequest<Result<DateTimeOffset>>;

internal sealed class PreviewDeadlineHandler(IAppDbContext db) : IRequestHandler<PreviewDeadlineQuery, Result<DateTimeOffset>>
{
    public async Task<Result<DateTimeOffset>> Handle(PreviewDeadlineQuery request, CancellationToken cancellationToken)
    {
        if (request.Minutes is < 0 or > 525_600)
            return Error.Validation("calendar.invalid_minutes", "Minutes must be between 0 and 525600.");
        var calendar = await db.BusinessCalendars.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return calendar is null
            ? SlaMapping.CalendarNotFound
            : new BusinessTimeCalculator(calendar).AddBusinessMinutes(request.Start, request.Minutes);
    }
}

public sealed record ListSlaPoliciesQuery : IRequest<IReadOnlyList<SlaPolicyDto>>;

internal sealed class ListSlaPoliciesHandler(IAppDbContext db) : IRequestHandler<ListSlaPoliciesQuery, IReadOnlyList<SlaPolicyDto>>
{
    public async Task<IReadOnlyList<SlaPolicyDto>> Handle(ListSlaPoliciesQuery request, CancellationToken cancellationToken)
    {
        var policies = await db.SlaPolicies.AsNoTracking().OrderByDescending(p => p.IsDefault).ThenBy(p => p.Name).ToListAsync(cancellationToken);
        var types = await db.RequestTypes.AsNoTracking().Select(t => new { t.Name, t.SlaPolicyId }).ToListAsync(cancellationToken);
        return policies.Select(p => SlaMapping.ToDto(p, types
            .Where(t => t.SlaPolicyId == p.Id || (t.SlaPolicyId == null && p.IsDefault))
            .Select(t => t.Name))).ToList();
    }
}
