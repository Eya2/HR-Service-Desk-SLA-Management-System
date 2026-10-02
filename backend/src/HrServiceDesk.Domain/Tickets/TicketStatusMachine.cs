namespace HrServiceDesk.Domain.Tickets;

/// <summary>Who is asking for a transition. A person can be several at once (an HR officer filing their own case).</summary>
[Flags]
public enum TransitionActor
{
    None = 0,

    /// <summary>The employee who submitted the case.</summary>
    Requester = 1,

    /// <summary>HR staff handling cases (HR Officer, Payroll Specialist, HR Admin).</summary>
    Agent = 2,

    /// <summary>The platform itself: approval workflow, automations.</summary>
    System = 4,
}

public enum TransitionCheck
{
    Allowed,
    NotPermitted,
    Invalid,
}

/// <summary>
/// The explicit table of allowed status changes and who may perform each one.
/// Anything not listed is an invalid transition. Closed, Rejected and Cancelled are terminal.
/// </summary>
public static class TicketStatusMachine
{
    private const TransitionActor Requester = TransitionActor.Requester;
    private const TransitionActor Agent = TransitionActor.Agent;
    private const TransitionActor System = TransitionActor.System;

    private static readonly Dictionary<(TicketStatus From, TicketStatus To), TransitionActor> Table = new()
    {
        [(TicketStatus.New, TicketStatus.PendingApproval)] = System,
        [(TicketStatus.New, TicketStatus.Open)] = Agent | System,
        [(TicketStatus.New, TicketStatus.Rejected)] = Agent,
        [(TicketStatus.New, TicketStatus.Cancelled)] = Requester | Agent,

        [(TicketStatus.PendingApproval, TicketStatus.Open)] = System,
        [(TicketStatus.PendingApproval, TicketStatus.Rejected)] = System,
        [(TicketStatus.PendingApproval, TicketStatus.Cancelled)] = Requester,

        [(TicketStatus.Open, TicketStatus.InProgress)] = Agent,
        [(TicketStatus.Open, TicketStatus.WaitingOnEmployee)] = Agent,
        [(TicketStatus.Open, TicketStatus.Resolved)] = Agent,
        [(TicketStatus.Open, TicketStatus.Rejected)] = Agent,
        [(TicketStatus.Open, TicketStatus.Cancelled)] = Requester | Agent,

        [(TicketStatus.InProgress, TicketStatus.Open)] = Agent,
        [(TicketStatus.InProgress, TicketStatus.WaitingOnEmployee)] = Agent,
        [(TicketStatus.InProgress, TicketStatus.Resolved)] = Agent,
        [(TicketStatus.InProgress, TicketStatus.Cancelled)] = Requester | Agent,

        [(TicketStatus.WaitingOnEmployee, TicketStatus.InProgress)] = Requester | Agent | System,
        [(TicketStatus.WaitingOnEmployee, TicketStatus.Resolved)] = Agent,
        [(TicketStatus.WaitingOnEmployee, TicketStatus.Cancelled)] = Requester | Agent,

        [(TicketStatus.Resolved, TicketStatus.Closed)] = Requester | Agent | System,
        [(TicketStatus.Resolved, TicketStatus.Reopened)] = Requester,

        [(TicketStatus.Reopened, TicketStatus.InProgress)] = Agent,
        [(TicketStatus.Reopened, TicketStatus.WaitingOnEmployee)] = Agent,
        [(TicketStatus.Reopened, TicketStatus.Resolved)] = Agent,
        [(TicketStatus.Reopened, TicketStatus.Cancelled)] = Requester | Agent,
    };

    public static IReadOnlyDictionary<(TicketStatus From, TicketStatus To), TransitionActor> Transitions => Table;

    public static bool IsTerminal(TicketStatus status) =>
        status is TicketStatus.Closed or TicketStatus.Rejected or TicketStatus.Cancelled;

    public static TransitionCheck Check(TicketStatus from, TicketStatus to, TransitionActor actor)
    {
        if (!Table.TryGetValue((from, to), out var allowed))
            return TransitionCheck.Invalid;
        return (allowed & actor) != TransitionActor.None ? TransitionCheck.Allowed : TransitionCheck.NotPermitted;
    }

    /// <summary>Statuses the actor may move a case to from <paramref name="from"/>, in table order.</summary>
    public static IReadOnlyList<TicketStatus> AvailableTo(TicketStatus from, TransitionActor actor) =>
        Table.Where(t => t.Key.From == from && (t.Value & actor) != TransitionActor.None).Select(t => t.Key.To).ToList();
}
