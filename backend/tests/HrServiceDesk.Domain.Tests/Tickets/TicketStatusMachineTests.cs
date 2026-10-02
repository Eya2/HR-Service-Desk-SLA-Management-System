using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Tickets;
using static HrServiceDesk.Domain.Tickets.TicketStatus;

namespace HrServiceDesk.Domain.Tests.Tickets;

public class TicketStatusMachineTests
{
    private const TransitionActor R = TransitionActor.Requester;
    private const TransitionActor A = TransitionActor.Agent;
    private const TransitionActor S = TransitionActor.System;

    /// <summary>The specification, written independently of the implementation's table.</summary>
    private static readonly Dictionary<(TicketStatus, TicketStatus), TransitionActor> Expected = new()
    {
        [(New, PendingApproval)] = S,
        [(New, Open)] = A | S,
        [(New, Rejected)] = A,
        [(New, Cancelled)] = R | A,
        [(PendingApproval, Open)] = S,
        [(PendingApproval, Rejected)] = S,
        [(PendingApproval, Cancelled)] = R,
        [(Open, InProgress)] = A,
        [(Open, WaitingOnEmployee)] = A,
        [(Open, Resolved)] = A,
        [(Open, Rejected)] = A,
        [(Open, Cancelled)] = R | A,
        [(InProgress, Open)] = A,
        [(InProgress, WaitingOnEmployee)] = A,
        [(InProgress, Resolved)] = A,
        [(InProgress, Cancelled)] = R | A,
        [(WaitingOnEmployee, InProgress)] = R | A | S,
        [(WaitingOnEmployee, Resolved)] = A,
        [(WaitingOnEmployee, Cancelled)] = R | A,
        [(Resolved, Closed)] = R | A | S,
        [(Resolved, Reopened)] = R,
        [(Reopened, InProgress)] = A,
        [(Reopened, WaitingOnEmployee)] = A,
        [(Reopened, Resolved)] = A,
        [(Reopened, Cancelled)] = R | A,
    };

    public static TheoryData<TicketStatus, TicketStatus, TransitionActor> EveryPairAndActor()
    {
        var data = new TheoryData<TicketStatus, TicketStatus, TransitionActor>();
        foreach (var from in Enum.GetValues<TicketStatus>())
        foreach (var to in Enum.GetValues<TicketStatus>())
        foreach (var actor in new[] { R, A, S })
            data.Add(from, to, actor);
        return data;
    }

    [Theory]
    [MemberData(nameof(EveryPairAndActor))]
    public void Every_status_pair_and_actor_behaves_as_specified(TicketStatus from, TicketStatus to, TransitionActor actor)
    {
        var expected = !Expected.TryGetValue((from, to), out var allowed)
            ? TransitionCheck.Invalid
            : (allowed & actor) != 0 ? TransitionCheck.Allowed : TransitionCheck.NotPermitted;

        TicketStatusMachine.Check(from, to, actor).Should().Be(expected);
    }

    [Fact]
    public void Table_has_exactly_the_specified_transitions() =>
        TicketStatusMachine.Transitions.Should().BeEquivalentTo(Expected);

    [Theory]
    [InlineData(Closed)]
    [InlineData(Rejected)]
    [InlineData(Cancelled)]
    public void Terminal_statuses_have_no_way_out(TicketStatus status)
    {
        TicketStatusMachine.IsTerminal(status).Should().BeTrue();
        TicketStatusMachine.AvailableTo(status, R | A | S).Should().BeEmpty();
    }

    [Fact]
    public void Every_non_terminal_status_can_be_left()
    {
        foreach (var status in Enum.GetValues<TicketStatus>().Where(s => !TicketStatusMachine.IsTerminal(s)))
            TicketStatusMachine.AvailableTo(status, R | A | S).Should().NotBeEmpty($"{status} must not be a dead end");
    }

    [Fact]
    public void A_person_holding_several_roles_gets_the_union()
    {
        TicketStatusMachine.AvailableTo(Resolved, R).Should().Equal(Closed, Reopened);
        TicketStatusMachine.AvailableTo(Resolved, A).Should().Equal(Closed);
        TicketStatusMachine.AvailableTo(New, R | A).Should().Equal(Open, Rejected, Cancelled);
    }

    private static readonly DateTimeOffset Now = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

    private static Ticket NewTicket() => Ticket.Submit(
        "HR-2026-000001",
        RequestType.Create("Leave", "", RequestCategory.LeaveAndAbsence, false, TicketPriority.Medium, FormSchema.Empty),
        Guid.NewGuid(), "Leave", null, "{}", Now);

    [Fact]
    public void Changing_status_records_an_audit_event()
    {
        var ticket = NewTicket();
        var agent = Guid.NewGuid();

        ticket.ChangeStatus(Open, A, agent, Now.AddMinutes(5));
        ticket.ChangeStatus(Rejected, A, agent, Now.AddMinutes(6), "  Not eligible  ");

        ticket.Status.Should().Be(Rejected);
        ticket.Events.Select(e => e.Type).Should().Equal(TicketEventType.Created, TicketEventType.StatusChanged, TicketEventType.StatusChanged);
        var last = ticket.Events.Last();
        last.ActorId.Should().Be(agent);
        last.OccurredAt.Should().Be(Now.AddMinutes(6));
        last.Data.Should().Be("""{"from":"Open","to":"Rejected","reason":"Not eligible"}""");
    }

    [Fact]
    public void Events_recorded_at_the_same_instant_keep_their_order()
    {
        var ticket = NewTicket();

        ticket.AddComment(Guid.NewGuid(), "Not eligible", isInternal: false, Now);
        ticket.ChangeStatus(Rejected, A, Guid.NewGuid(), Now, "Not eligible");

        ticket.Events.Select(e => e.OccurredAt).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
    }

    [Fact]
    public void Invalid_and_unauthorised_transitions_are_rejected_without_side_effects()
    {
        var ticket = NewTicket();

        var invalid = () => ticket.ChangeStatus(Closed, A, Guid.NewGuid(), Now);
        invalid.Should().Throw<DomainException>().Which.Code.Should().Be("ticket.invalid_transition");

        var forbidden = () => ticket.ChangeStatus(Rejected, R, Guid.NewGuid(), Now);
        forbidden.Should().Throw<DomainException>().Which.Code.Should().Be("ticket.transition_not_permitted");

        ticket.Status.Should().Be(New);
        ticket.Events.Should().ContainSingle();
    }

    [Fact]
    public void Internal_notes_produce_internal_events()
    {
        var ticket = NewTicket();

        ticket.AddComment(Guid.NewGuid(), "HR only", isInternal: true, Now);
        ticket.AddComment(Guid.NewGuid(), "Hello", isInternal: false, Now);

        ticket.Events.Where(e => e.Type == TicketEventType.CommentAdded).Select(e => e.IsInternal).Should().Equal(true, false);
    }

    [Fact]
    public void Edits_are_audited_only_when_something_changes()
    {
        var ticket = NewTicket();
        var user = Guid.NewGuid();

        ticket.UpdateDetails("Leave", null, user, Now);
        ticket.ChangePriority(TicketPriority.Medium, user, Now);
        ticket.Events.Should().ContainSingle();

        ticket.UpdateDetails("Annual leave", null, user, Now);
        ticket.ChangePriority(TicketPriority.High, user, Now);
        ticket.Events.Select(e => e.Type).Should().Equal(
            TicketEventType.Created, TicketEventType.DetailsUpdated, TicketEventType.PriorityChanged);
    }
}
