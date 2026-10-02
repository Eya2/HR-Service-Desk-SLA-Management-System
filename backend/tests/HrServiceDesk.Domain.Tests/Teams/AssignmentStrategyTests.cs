using HrServiceDesk.Domain.Teams;

namespace HrServiceDesk.Domain.Tests.Teams;

public class AssignmentStrategyTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-00000000000c");
    private static readonly Dictionary<Guid, int> NoLoad = [];

    private static Guid? Pick(AssignmentStrategy strategy, Guid? last, Dictionary<Guid, int>? load = null, params Guid[] members) =>
        AssignmentPickers.For(strategy).Pick(new AssignmentContext(members, load ?? NoLoad, last));

    [Fact]
    public void Manual_never_assigns() => Pick(AssignmentStrategy.Manual, null, null, A, B).Should().BeNull();

    [Theory]
    [InlineData(AssignmentStrategy.RoundRobin)]
    [InlineData(AssignmentStrategy.LeastLoaded)]
    public void Empty_teams_assign_nobody(AssignmentStrategy strategy) => Pick(strategy, null).Should().BeNull();

    [Fact]
    public void Round_robin_takes_turns_and_wraps_around()
    {
        Pick(AssignmentStrategy.RoundRobin, null, null, A, B, C).Should().Be(A);
        Pick(AssignmentStrategy.RoundRobin, A, null, A, B, C).Should().Be(B);
        Pick(AssignmentStrategy.RoundRobin, C, null, A, B, C).Should().Be(A);
    }

    [Fact]
    public void Round_robin_restarts_when_the_last_assignee_left_the_team() =>
        Pick(AssignmentStrategy.RoundRobin, Guid.NewGuid(), null, A, B).Should().Be(A);

    [Fact]
    public void Least_loaded_picks_the_member_with_fewest_active_cases()
    {
        var load = new Dictionary<Guid, int> { [A] = 4, [B] = 1, [C] = 2 };

        Pick(AssignmentStrategy.LeastLoaded, null, load, A, B, C).Should().Be(B);
    }

    [Fact]
    public void Least_loaded_breaks_ties_in_round_robin_order()
    {
        var load = new Dictionary<Guid, int> { [A] = 1, [B] = 1, [C] = 1 };

        Pick(AssignmentStrategy.LeastLoaded, A, load, A, B, C).Should().Be(B);
        Pick(AssignmentStrategy.LeastLoaded, B, load, A, B, C).Should().Be(C);
    }

    [Fact]
    public void Members_without_cases_count_as_zero() =>
        Pick(AssignmentStrategy.LeastLoaded, null, new Dictionary<Guid, int> { [A] = 3 }, A, B).Should().Be(B);

    [Fact]
    public void Team_rotates_among_eligible_members_and_remembers_the_last_one()
    {
        var team = Team.Create("Payroll", AssignmentStrategy.RoundRobin, [A, B, C]);
        Guid[] eligible = [A, C]; // B is inactive

        var picks = Enumerable.Range(0, 4).Select(_ => team.PickAssignee(eligible, NoLoad)).ToList();

        picks.Should().Equal(A, C, A, C);
        team.LastAssignedUserId.Should().Be(C);
    }

    [Fact]
    public void Updating_members_keeps_their_order_and_drops_a_stale_pointer()
    {
        var team = Team.Create("HR", AssignmentStrategy.RoundRobin, [A, B]);
        team.PickAssignee([A, B], NoLoad);
        team.PickAssignee([A, B], NoLoad);
        team.LastAssignedUserId.Should().Be(B);

        team.Update("HR", AssignmentStrategy.RoundRobin, [C, A]);

        team.OrderedMemberIds.Should().Equal(A, C);
        team.LastAssignedUserId.Should().BeNull();
    }
}
