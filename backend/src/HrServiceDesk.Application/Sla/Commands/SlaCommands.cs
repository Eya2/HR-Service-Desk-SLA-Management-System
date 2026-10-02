using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Application.Sla.Queries;
using HrServiceDesk.Application.Tickets;
using HrServiceDesk.Domain.Sla;
using HrServiceDesk.Domain.Tickets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Sla.Commands;

/// <summary>Replaces the calendar's name, time zone and weekly working intervals.</summary>
public sealed record UpdateCalendarCommand(string Name, string TimeZoneId, IReadOnlyList<WorkingIntervalDto> WorkingHours) : IRequest<Result<CalendarDto>>;

internal sealed class UpdateCalendarValidator : AbstractValidator<UpdateCalendarCommand>
{
    public UpdateCalendarValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(BusinessCalendar.NameMaxLength);
        RuleFor(c => c.TimeZoneId).NotEmpty();
        RuleFor(c => c.WorkingHours).NotEmpty();
        RuleForEach(c => c.WorkingHours).ChildRules(i =>
        {
            i.RuleFor(x => x.Day).Must(d => Enum.TryParse<DayOfWeek>(d, ignoreCase: false, out _)).WithMessage("Unknown day.");
            i.RuleFor(x => x.Start).Must(t => TimeOnly.TryParse(t, System.Globalization.CultureInfo.InvariantCulture, out _)).WithMessage("Use HH:mm.");
            i.RuleFor(x => x.End).Must(t => TimeOnly.TryParse(t, System.Globalization.CultureInfo.InvariantCulture, out _)).WithMessage("Use HH:mm.");
        });
    }
}

/// <summary>Adds (<see cref="Remove"/> false) or removes a public holiday.</summary>
public sealed record ChangeHolidayCommand(DateOnly Date, string? Name, bool Remove) : IRequest<Result<CalendarDto>>;

/// <summary>Creates (<see cref="Id"/> null) or updates an SLA policy.</summary>
public sealed record SaveSlaPolicyCommand(
    Guid? Id, string Name, int AtRiskThresholdPercent, IReadOnlyList<SlaTargetDto> Targets, IReadOnlyList<string> PauseStatuses, bool IsDefault)
    : IRequest<Result<SlaPolicyDto>>;

internal sealed class SaveSlaPolicyValidator : AbstractValidator<SaveSlaPolicyCommand>
{
    public SaveSlaPolicyValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(SlaPolicy.NameMaxLength);
        RuleFor(c => c.Targets).NotEmpty();
        RuleForEach(c => c.Targets).Must(t => Enum.TryParse<TicketPriority>(t.Priority, ignoreCase: false, out _)).WithMessage("Unknown priority.");
        RuleForEach(c => c.PauseStatuses).Must(s => Enum.TryParse<TicketStatus>(s, ignoreCase: false, out _)).WithMessage("Unknown status.");
    }
}

/// <summary>Chooses a request type's SLA policy (null: the default policy).</summary>
public sealed record SetRequestTypeSlaPolicyCommand(Guid RequestTypeId, Guid? PolicyId) : IRequest<Result>;

internal sealed class SlaCommandHandlers(IAppDbContext db, ISender sender)
    : IRequestHandler<UpdateCalendarCommand, Result<CalendarDto>>,
      IRequestHandler<ChangeHolidayCommand, Result<CalendarDto>>,
      IRequestHandler<SaveSlaPolicyCommand, Result<SlaPolicyDto>>,
      IRequestHandler<SetRequestTypeSlaPolicyCommand, Result>
{
    public async Task<Result<CalendarDto>> Handle(UpdateCalendarCommand request, CancellationToken cancellationToken)
    {
        var calendar = await db.BusinessCalendars.FirstOrDefaultAsync(cancellationToken);
        if (calendar is null)
            return SlaMapping.CalendarNotFound;

        var intervals = request.WorkingHours.Select(i => new WorkingInterval(
            Enum.Parse<DayOfWeek>(i.Day),
            TimeOnly.Parse(i.Start, System.Globalization.CultureInfo.InvariantCulture),
            TimeOnly.Parse(i.End, System.Globalization.CultureInfo.InvariantCulture)));
        calendar.Update(request.Name, request.TimeZoneId, intervals);
        await db.SaveChangesAsync(cancellationToken);
        return SlaMapping.ToDto(calendar);
    }

    public async Task<Result<CalendarDto>> Handle(ChangeHolidayCommand request, CancellationToken cancellationToken)
    {
        var calendar = await db.BusinessCalendars.FirstOrDefaultAsync(cancellationToken);
        if (calendar is null)
            return SlaMapping.CalendarNotFound;

        if (request.Remove)
            calendar.RemoveHoliday(request.Date);
        else
            calendar.AddHoliday(request.Date, request.Name ?? string.Empty);
        await db.SaveChangesAsync(cancellationToken);
        return SlaMapping.ToDto(calendar);
    }

    public async Task<Result<SlaPolicyDto>> Handle(SaveSlaPolicyCommand request, CancellationToken cancellationToken)
    {
        var targets = request.Targets.Select(t => new SlaTarget(Enum.Parse<TicketPriority>(t.Priority), t.FirstResponseMinutes, t.ResolutionMinutes));
        var pauses = request.PauseStatuses.Select(Enum.Parse<TicketStatus>);

        SlaPolicy policy;
        if (request.Id is { } id)
        {
            var existing = await db.SlaPolicies.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (existing is null)
                return SlaMapping.PolicyNotFound;
            policy = existing;
            policy.Update(request.Name, targets, request.AtRiskThresholdPercent, pauses);
        }
        else
        {
            policy = SlaPolicy.Create(request.Name, targets, request.AtRiskThresholdPercent, pauses);
            db.SlaPolicies.Add(policy);
        }

        // Exactly one default policy per organisation.
        if (request.IsDefault)
        {
            foreach (var other in await db.SlaPolicies.Where(p => p.IsDefault && p.Id != policy.Id).ToListAsync(cancellationToken))
                other.MarkAsDefault(false);
            policy.MarkAsDefault(true);
        }
        else if (policy.IsDefault)
        {
            return Error.DomainRule("sla.default_required", "Choose another default policy before unmarking this one.");
        }

        await db.SaveChangesAsync(cancellationToken);
        var all = await sender.Send(new ListSlaPoliciesQuery(), cancellationToken);
        return all.Single(p => p.Id == policy.Id);
    }

    public async Task<Result> Handle(SetRequestTypeSlaPolicyCommand request, CancellationToken cancellationToken)
    {
        var type = await db.RequestTypes.SingleOrDefaultAsync(t => t.Id == request.RequestTypeId, cancellationToken);
        if (type is null)
            return TicketErrors.RequestTypeNotFound;
        if (request.PolicyId is { } policyId && !await db.SlaPolicies.AnyAsync(p => p.Id == policyId, cancellationToken))
            return SlaMapping.PolicyNotFound;

        type.SetSlaPolicy(request.PolicyId);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
