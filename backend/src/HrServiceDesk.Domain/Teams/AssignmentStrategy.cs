namespace HrServiceDesk.Domain.Teams;

/// <summary>How a team hands out new cases.</summary>
public enum AssignmentStrategy
{
    /// <summary>Cases wait in the team queue until someone takes or assigns them.</summary>
    Manual,

    /// <summary>Members take turns, in a stable order.</summary>
    RoundRobin,

    /// <summary>The member with the fewest active cases gets the next one (ties go round-robin).</summary>
    LeastLoaded,
}

/// <summary>Everything a strategy needs to pick an assignee. Members are in a stable order.</summary>
public sealed record AssignmentContext(
    IReadOnlyList<Guid> Members,
    IReadOnlyDictionary<Guid, int> ActiveLoad,
    Guid? LastAssignedUserId);

public interface IAssignmentPicker
{
    /// <summary>The member to assign, or null to leave the case in the team queue.</summary>
    Guid? Pick(AssignmentContext context);
}

/// <summary>Strategy pattern: one pure picker per <see cref="AssignmentStrategy"/>.</summary>
public static class AssignmentPickers
{
    public static IAssignmentPicker For(AssignmentStrategy strategy) => strategy switch
    {
        AssignmentStrategy.Manual => ManualPicker.Instance,
        AssignmentStrategy.RoundRobin => RoundRobinPicker.Instance,
        AssignmentStrategy.LeastLoaded => LeastLoadedPicker.Instance,
        _ => throw new ArgumentOutOfRangeException(nameof(strategy)),
    };

    private sealed class ManualPicker : IAssignmentPicker
    {
        public static readonly ManualPicker Instance = new();

        public Guid? Pick(AssignmentContext context) => null;
    }

    private sealed class RoundRobinPicker : IAssignmentPicker
    {
        public static readonly RoundRobinPicker Instance = new();

        public Guid? Pick(AssignmentContext context)
        {
            var ordered = InTurnOrder(context);
            return ordered.Count == 0 ? null : ordered[0];
        }
    }

    private sealed class LeastLoadedPicker : IAssignmentPicker
    {
        public static readonly LeastLoadedPicker Instance = new();

        public Guid? Pick(AssignmentContext context)
        {
            var ordered = InTurnOrder(context);
            if (ordered.Count == 0)
                return null;
            // MinBy keeps the first of equals, so ties follow the round-robin order.
            return ordered.MinBy(m => context.ActiveLoad.GetValueOrDefault(m));
        }
    }

    /// <summary>Members starting with the one after the last assignee (wrapping around).</summary>
    private static List<Guid> InTurnOrder(AssignmentContext context)
    {
        var members = context.Members.Distinct().ToList();
        if (members.Count == 0)
            return members;
        var last = context.LastAssignedUserId is { } id ? members.IndexOf(id) : -1;
        var start = (last + 1) % members.Count;
        return [.. members.Skip(start), .. members.Take(start)];
    }
}
