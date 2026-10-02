using HrServiceDesk.Application.Abstractions;
using HrServiceDesk.Application.Tickets.Assignment;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HrServiceDesk.Application.Teams.Queries;

/// <summary>Teams with their members' current workload and the request types they handle.</summary>
public sealed record ListTeamsQuery : IRequest<IReadOnlyList<TeamDto>>;

internal sealed class ListTeamsHandler(IAppDbContext db) : IRequestHandler<ListTeamsQuery, IReadOnlyList<TeamDto>>
{
    public async Task<IReadOnlyList<TeamDto>> Handle(ListTeamsQuery request, CancellationToken cancellationToken)
    {
        var teams = await db.Teams.AsNoTracking().OrderBy(t => t.Name).ToListAsync(cancellationToken);
        var memberIds = teams.SelectMany(t => t.OrderedMemberIds).Distinct().ToList();

        var names = await db.Users.AsNoTracking().Where(u => memberIds.Contains(u.Id))
            .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName })
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);
        var load = await db.Tickets.AsNoTracking()
            .Where(t => t.AssigneeId != null && memberIds.Contains(t.AssigneeId.Value) && AutoAssigner.ActiveStatuses.Contains(t.Status))
            .GroupBy(t => t.AssigneeId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);
        var types = await db.RequestTypes.AsNoTracking().Where(t => t.ResponsibleTeamId != null)
            .Select(t => new { TeamId = t.ResponsibleTeamId!.Value, t.Name })
            .ToListAsync(cancellationToken);

        return teams.Select(team => new TeamDto(
            team.Id,
            team.Name,
            team.Strategy.ToString(),
            team.OrderedMemberIds.Where(names.ContainsKey)
                .Select(id => new TeamMemberDto(id, names[id], load.GetValueOrDefault(id)))
                .ToList(),
            types.Where(t => t.TeamId == team.Id).Select(t => t.Name).Order().ToList())).ToList();
    }
}
