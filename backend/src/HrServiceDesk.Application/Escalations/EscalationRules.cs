using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Domain.Escalations;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Escalations;

public sealed record EscalationRuleDto(
    Guid Id, string Name, string Trigger, int? NoResponseHours, string Action, Guid? TargetTeamId, Guid? RequestTypeId, bool IsActive, int TimesFired);

public sealed record ListEscalationRulesQuery : IRequest<IReadOnlyList<EscalationRuleDto>>;

/// <summary>Creates (<see cref="Id"/> null) or updates an escalation rule.</summary>
public sealed record SaveEscalationRuleCommand(
    Guid? Id, string Name, string Trigger, int? NoResponseHours, string Action, Guid? TargetTeamId, Guid? RequestTypeId, bool IsActive)
    : IRequest<Result<EscalationRuleDto>>;

internal sealed class SaveEscalationRuleValidator : AbstractValidator<SaveEscalationRuleCommand>
{
    public SaveEscalationRuleValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(EscalationRule.NameMaxLength);
        RuleFor(c => c.Trigger).Must(t => Enum.TryParse<EscalationTrigger>(t, ignoreCase: false, out _))
            .WithMessage($"Trigger must be one of: {string.Join(", ", Enum.GetNames<EscalationTrigger>())}.");
        RuleFor(c => c.Action).Must(a => Enum.TryParse<EscalationAction>(a, ignoreCase: false, out _))
            .WithMessage($"Action must be one of: {string.Join(", ", Enum.GetNames<EscalationAction>())}.");
    }
}

internal sealed class EscalationRuleHandlers(IAppDbContext db, ISender sender)
    : IRequestHandler<ListEscalationRulesQuery, IReadOnlyList<EscalationRuleDto>>,
      IRequestHandler<SaveEscalationRuleCommand, Result<EscalationRuleDto>>
{
    public async Task<IReadOnlyList<EscalationRuleDto>> Handle(ListEscalationRulesQuery request, CancellationToken cancellationToken)
    {
        var counts = await db.EscalationExecutions.GroupBy(x => x.RuleId)
            .Select(g => new { RuleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RuleId, x => x.Count, cancellationToken);
        var rules = await db.EscalationRules.AsNoTracking().OrderBy(r => r.Trigger).ThenBy(r => r.Name).ToListAsync(cancellationToken);
        return rules.Select(r => new EscalationRuleDto(
            r.Id, r.Name, r.Trigger.ToString(), r.NoResponseHours, r.Action.ToString(), r.TargetTeamId, r.RequestTypeId, r.IsActive,
            counts.GetValueOrDefault(r.Id))).ToList();
    }

    public async Task<Result<EscalationRuleDto>> Handle(SaveEscalationRuleCommand request, CancellationToken cancellationToken)
    {
        if (request.TargetTeamId is { } teamId && !await db.Teams.AnyAsync(t => t.Id == teamId, cancellationToken))
            return Error.Validation("escalation.team_not_found", "The target team does not exist.");
        if (request.RequestTypeId is { } typeId && !await db.RequestTypes.AnyAsync(t => t.Id == typeId, cancellationToken))
            return Error.Validation("escalation.request_type_not_found", "The request type does not exist.");

        var trigger = Enum.Parse<EscalationTrigger>(request.Trigger);
        var action = Enum.Parse<EscalationAction>(request.Action);
        EscalationRule rule;
        if (request.Id is { } id)
        {
            var existing = await db.EscalationRules.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (existing is null)
                return Error.NotFound("escalation.not_found", "Escalation rule not found.");
            rule = existing;
            rule.Update(request.Name, trigger, request.NoResponseHours, action, request.TargetTeamId, request.RequestTypeId, request.IsActive);
        }
        else
        {
            rule = EscalationRule.Create(request.Name, trigger, request.NoResponseHours, action, request.TargetTeamId, request.RequestTypeId);
            if (!request.IsActive)
                rule.Update(request.Name, trigger, request.NoResponseHours, action, request.TargetTeamId, request.RequestTypeId, isActive: false);
            db.EscalationRules.Add(rule);
        }

        await db.SaveChangesAsync(cancellationToken);
        return (await sender.Send(new ListEscalationRulesQuery(), cancellationToken)).Single(r => r.Id == rule.Id);
    }
}
