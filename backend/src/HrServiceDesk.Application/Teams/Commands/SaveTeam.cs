using FluentValidation;
using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Auth;
using HrServiceDesk.Application.Common.Results;
using HrServiceDesk.Application.Teams.Queries;
using HrServiceDesk.Application.Tickets;
using HrServiceDesk.Domain.Teams;
using HrServiceDesk.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Teams.Commands;

/// <summary>Creates (<see cref="Id"/> null) or updates a team. Members must be active HR staff.</summary>
public sealed record SaveTeamCommand(Guid? Id, string Name, string Strategy, IReadOnlyList<Guid> MemberIds) : IRequest<Result<TeamDto>>;

internal sealed class SaveTeamValidator : AbstractValidator<SaveTeamCommand>
{
    public SaveTeamValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(Team.NameMaxLength);
        RuleFor(c => c.Strategy).Must(s => Enum.TryParse<AssignmentStrategy>(s, ignoreCase: false, out _))
            .WithMessage($"Strategy must be one of: {string.Join(", ", Enum.GetNames<AssignmentStrategy>())}.");
        RuleFor(c => c.MemberIds).NotNull().Must(m => m.Count <= Team.MaxMembers);
    }
}

internal sealed class SaveTeamHandler(IAppDbContext db, ISender sender) : IRequestHandler<SaveTeamCommand, Result<TeamDto>>
{
    private static readonly Error InvalidMembers =
        Error.Validation("team.invalid_members", "Team members must be active HR staff of this organisation.");

    private static readonly Role[] StaffRoles = [.. Policies.RolesByPolicy[Policies.CanWorkTickets]];

    public async Task<Result<TeamDto>> Handle(SaveTeamCommand request, CancellationToken cancellationToken)
    {
        var ids = request.MemberIds.Distinct().ToList();
        var valid = await db.Users.CountAsync(u => ids.Contains(u.Id) && u.IsActive && u.Roles.Any(r => StaffRoles.Contains(r)), cancellationToken);
        if (valid != ids.Count)
            return InvalidMembers;

        var strategy = Enum.Parse<AssignmentStrategy>(request.Strategy);
        Team team;
        if (request.Id is { } id)
        {
            var existing = await db.Teams.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
            if (existing is null)
                return TicketErrors.TeamNotFound;
            team = existing;
            team.Update(request.Name, strategy, ids);
        }
        else
        {
            team = Team.Create(request.Name, strategy, ids);
            db.Teams.Add(team);
        }

        await db.SaveChangesAsync(cancellationToken);
        var teams = await sender.Send(new ListTeamsQuery(), cancellationToken);
        return teams.Single(t => t.Id == team.Id);
    }
}

/// <summary>Chooses the team that handles a request type (null: none).</summary>
public sealed record SetResponsibleTeamCommand(Guid RequestTypeId, Guid? TeamId) : IRequest<Result>;

internal sealed class SetResponsibleTeamHandler(IAppDbContext db) : IRequestHandler<SetResponsibleTeamCommand, Result>
{
    public async Task<Result> Handle(SetResponsibleTeamCommand request, CancellationToken cancellationToken)
    {
        var type = await db.RequestTypes.SingleOrDefaultAsync(t => t.Id == request.RequestTypeId, cancellationToken);
        if (type is null)
            return TicketErrors.RequestTypeNotFound;
        if (request.TeamId is { } teamId && !await db.Teams.AnyAsync(t => t.Id == teamId, cancellationToken))
            return TicketErrors.TeamNotFound;

        type.SetResponsibleTeam(request.TeamId);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
