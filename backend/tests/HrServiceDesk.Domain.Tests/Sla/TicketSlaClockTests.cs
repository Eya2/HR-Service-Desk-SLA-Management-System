using HrServiceDesk.Domain.Catalog;
using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Sla;
using HrServiceDesk.Domain.Tickets;
using HrServiceDesk.Domain.Users;
using HrServiceDesk.Domain.Workflows;
using static HrServiceDesk.Domain.Tickets.TicketStatus;

namespace HrServiceDesk.Domain.Tests.Sla;

public class TicketSlaClockTests
{
    /// <summary>Business time equals wall-clock time, so the clock rules are tested in isolation.</summary>
    private sealed class WallClock : IBusinessTimeCalculator
    {
        public DateTimeOffset AddBusinessMinutes(DateTimeOffset start, int minutes) => start.AddMinutes(minutes);

        public int BusinessMinutesBetween(DateTimeOffset from, DateTimeOffset to) => to > from ? (int)(to - from).TotalMinutes : 0;
    }

    private static readonly WallClock Clock = new();
    private static readonly DateTimeOffset T0 = new(2026, 3, 16, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Agent = Guid.NewGuid();

    // Medium: first response 60 min, resolution 240 min. High: 30 / 120.
    private static readonly SlaPolicy Policy = SlaPolicy.Create(
        "Standard",
        [
            new SlaTarget(TicketPriority.Low, 120, 480),
            new SlaTarget(TicketPriority.Medium, 60, 240),
            new SlaTarget(TicketPriority.High, 30, 120),
            new SlaTarget(TicketPriority.Critical, 15, 60),
        ],
        80,
        [PendingApproval, WaitingOnEmployee]);

    private static Ticket StartedTicket()
    {
        var type = RequestType.Create("Leave", "", RequestCategory.LeaveAndAbsence, false, TicketPriority.Medium, FormSchema.Empty);
        var ticket = Ticket.Submit("HR-2026-000001", type, Guid.NewGuid(), "Leave", null, "{}", T0);
        ticket.StartSla(Policy, T0);
        ticket.RecalculateSla(Clock, T0);
        return ticket;
    }

    private static DateTimeOffset At(int minutes) => T0.AddMinutes(minutes);

    [Fact]
    public void Clock_starts_on_track_with_deadlines_from_the_priority_targets()
    {
        var ticket = StartedTicket();

        ticket.SlaState.Should().Be(SlaState.OnTrack);
        ticket.FirstResponseDueAt.Should().Be(At(60));
        ticket.ResolutionDueAt.Should().Be(At(240));
        ticket.Events.Should().NotContain(e => e.Type == TicketEventType.SlaStateChanged, "starting the clock is not a change of state");
    }

    [Fact]
    public void Goes_at_risk_at_80_percent_then_breached_and_records_each_change()
    {
        var ticket = StartedTicket();
        ticket.ChangeStatus(Open, TransitionActor.Agent, Agent, At(5));
        ticket.ChangeStatus(InProgress, TransitionActor.Agent, Agent, At(10)); // first response

        ticket.RecalculateSla(Clock, At(191)).Current.Should().Be(SlaState.OnTrack);
        ticket.RecalculateSla(Clock, At(192)).Should().Be((SlaState.OnTrack, SlaState.AtRisk));
        ticket.RecalculateSla(Clock, At(240)).Should().Be((SlaState.AtRisk, SlaState.Breached));
        ticket.RecalculateSla(Clock, At(300)).Should().Be((SlaState.Breached, SlaState.Breached));

        ticket.ResolutionBreached.Should().BeTrue();
        ticket.FirstResponseBreached.Should().BeFalse();
        ticket.Events.Where(e => e.Type == TicketEventType.SlaStateChanged).Select(e => e.Data)
            .Should().Equal("""{"from":"OnTrack","to":"AtRisk"}""", """{"from":"AtRisk","to":"Breached"}""");
    }

    [Fact]
    public void A_missing_first_response_drives_the_state_before_the_resolution_target()
    {
        var ticket = StartedTicket();

        ticket.RecalculateSla(Clock, At(48)).Current.Should().Be(SlaState.AtRisk, "48 of 60 minutes without response");
        ticket.RecalculateSla(Clock, At(61)).Current.Should().Be(SlaState.Breached);
        ticket.FirstResponseBreached.Should().BeTrue();
    }

    [Fact]
    public void Waiting_on_the_employee_pauses_the_clock_and_pushes_the_deadline()
    {
        var ticket = StartedTicket();
        ticket.ChangeStatus(Open, TransitionActor.Agent, Agent, At(0));
        ticket.ChangeStatus(WaitingOnEmployee, TransitionActor.Agent, Agent, At(30));

        ticket.RecalculateSla(Clock, At(500)).Current.Should().Be(SlaState.OnTrack, "the clock did not run while waiting");
        ticket.IsSlaPaused.Should().BeTrue();
        ticket.ResolutionDueAt.Should().BeNull();

        ticket.ChangeStatus(InProgress, TransitionActor.Requester, ticket.RequesterId, At(530));
        ticket.RecalculateSla(Clock, At(530));

        ticket.ResolutionDueAt.Should().Be(At(740), "240 minutes target + 500 paused");
        ticket.SlaPauses.Should().ContainSingle().Which.Should().Be(new SlaPause(At(30), At(530)));
    }

    [Fact]
    public void Pending_approval_pauses_the_clock_from_submission()
    {
        var type = RequestType.Create("Advance", "", RequestCategory.Payroll, false, TicketPriority.Medium, FormSchema.Empty);
        var ticket = Ticket.Submit("HR-2026-000002", type, Guid.NewGuid(), "Advance", null, "{}", T0);
        ticket.StartApproval(WorkflowDefinition.Create(type.Id, false, [("Manager approval", Role.HrAdmin)]), null, T0);
        ticket.StartSla(Policy, T0);

        ticket.RecalculateSla(Clock, At(600)).Current.Should().Be(SlaState.OnTrack);
        ticket.FirstResponseDueAt.Should().BeNull();

        ticket.DecideApproval(ticket.Approvals.Single().Id, approve: true, Guid.NewGuid(), [Role.HrAdmin], null, At(600));
        ticket.RecalculateSla(Clock, At(600));
        ticket.ResolutionDueAt.Should().Be(At(840));
        ticket.FirstResponseDueAt.Should().Be(At(660));
    }

    [Fact]
    public void Resolving_stops_the_clock_and_reopening_restarts_it()
    {
        var ticket = StartedTicket();
        ticket.ChangeStatus(Open, TransitionActor.Agent, Agent, At(0));
        ticket.ChangeStatus(Resolved, TransitionActor.Agent, Agent, At(100));

        ticket.RecalculateSla(Clock, At(1000)).Current.Should().Be(SlaState.OnTrack, "resolved after 100 of 240 minutes");
        ticket.FirstRespondedAt.Should().Be(At(100), "resolving answers the employee");

        ticket.ResolvedAt.Should().Be(At(100));
        ticket.ChangeStatus(Reopened, TransitionActor.Requester, ticket.RequesterId, At(1000));
        ticket.ResolvedAt.Should().BeNull();
        ticket.ReopenCount.Should().Be(1);
        ticket.RecalculateSla(Clock, At(1000)).Current.Should().Be(SlaState.OnTrack);
        ticket.ResolutionDueAt.Should().Be(At(1140), "140 minutes were left");
    }

    [Fact]
    public void Closing_freezes_the_result()
    {
        var ticket = StartedTicket();
        ticket.ChangeStatus(Open, TransitionActor.Agent, Agent, At(0));
        ticket.ChangeStatus(Resolved, TransitionActor.Agent, Agent, At(250));
        ticket.ChangeStatus(Closed, TransitionActor.Requester, ticket.RequesterId, At(300));

        ticket.RecalculateSla(Clock, At(5000)).Current.Should().Be(SlaState.Breached);
        ticket.ResolutionDueAt.Should().Be(At(240));
        ticket.SlaStoppedAt.Should().Be(At(300));
    }

    [Fact]
    public void A_priority_change_takes_the_new_targets()
    {
        var ticket = StartedTicket();
        ticket.ChangePriority(TicketPriority.High, Agent, At(10));

        ticket.Retarget(Policy);
        ticket.RecalculateSla(Clock, At(10));

        ticket.ResolutionDueAt.Should().Be(At(120));
    }

    [Fact]
    public void Policies_are_validated()
    {
        var missing = () => SlaPolicy.Create("Partial", [new SlaTarget(TicketPriority.Low, 60, 120)], 80, []);
        missing.Should().Throw<DomainException>().Which.Code.Should().Be("sla.missing_targets");

        var badPause = () => SlaPolicy.Create("Bad", Policy.Targets, 80, [Open]);
        badPause.Should().Throw<DomainException>().Which.Code.Should().Be("sla.invalid_pause_status");

        var inverted = () => SlaPolicy.Create("Inverted", Policy.Targets.Select(t => t with { FirstResponseMinutes = t.ResolutionMinutes + 1 }), 80, []);
        inverted.Should().Throw<DomainException>().Which.Code.Should().Be("sla.invalid_targets");
    }
}
