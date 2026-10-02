using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Tickets;

namespace HrServiceDesk.Domain.Tests.Teams;

public class TicketAssignmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

    private static Ticket NewTicket(Guid? teamId = null)
    {
        var type = RequestType.Create("Leave", "", RequestCategory.LeaveAndAbsence, false, TicketPriority.Medium, FormSchema.Empty);
        type.SetResponsibleTeam(teamId ?? Guid.NewGuid());
        return Ticket.Submit("HR-2026-000001", type, Guid.NewGuid(), "Leave", null, "{}", Now);
    }

    [Fact]
    public void Cases_are_routed_to_the_responsible_team()
    {
        var teamId = Guid.NewGuid();

        NewTicket(teamId).TeamId.Should().Be(teamId);
    }

    [Fact]
    public void Claiming_a_free_case_assigns_it_and_is_audited()
    {
        var ticket = NewTicket();
        var agent = Guid.NewGuid();

        ticket.Claim(agent, Now);
        ticket.Claim(agent, Now); // idempotent for the same agent

        ticket.AssigneeId.Should().Be(agent);
        ticket.Events.Count(e => e.Type == TicketEventType.Assigned).Should().Be(1);
    }

    [Fact]
    public void A_case_held_by_someone_else_cannot_be_claimed()
    {
        var ticket = NewTicket();
        ticket.Claim(Guid.NewGuid(), Now);

        var act = () => ticket.Claim(Guid.NewGuid(), Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("ticket.already_assigned");
    }

    [Fact]
    public void Moving_to_another_team_releases_the_assignee()
    {
        var ticket = NewTicket();
        ticket.Assign(Guid.NewGuid(), null, Now);
        var payroll = Guid.NewGuid();

        ticket.MoveToTeam(payroll, Guid.NewGuid(), Now);

        ticket.TeamId.Should().Be(payroll);
        ticket.AssigneeId.Should().BeNull();
    }

    [Fact]
    public void Closed_cases_cannot_be_reassigned()
    {
        var ticket = NewTicket();
        ticket.ChangeStatus(TicketStatus.Cancelled, TransitionActor.Requester, ticket.RequesterId, Now);

        var act = () => ticket.Assign(Guid.NewGuid(), null, Now);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("ticket.not_assignable");
    }
}
