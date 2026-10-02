using HrServiceDesk.Domain.Common;

namespace HrServiceDesk.Domain.Teams;

/// <summary>A group of HR staff handling some request types, with its own assignment strategy.</summary>
public sealed class Team : Entity, ITenantOwned, IAuditable
{
    public const int NameMaxLength = 100;
    public const int MaxMembers = 50;

    private readonly List<TeamMember> _members = [];

    private Team() { }

    public Guid TenantId { get; set; }

    public string Name { get; private set; } = string.Empty;

    public AssignmentStrategy Strategy { get; private set; } = AssignmentStrategy.Manual;

    public IReadOnlyCollection<TeamMember> Members => _members.AsReadOnly();

    /// <summary>
    /// Marks the restricted HR group: its members (and the requester) are the only people who can see
    /// confidential cases.
    /// </summary>
    public bool IsConfidentialGroup { get; private set; }

    /// <summary>Who received the last automatically assigned case (round-robin pointer).</summary>
    public Guid? LastAssignedUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Members in a stable order (when they joined, then id).</summary>
    public IReadOnlyList<Guid> OrderedMemberIds => _members.OrderBy(m => m.JoinedOrder).ThenBy(m => m.UserId).Select(m => m.UserId).ToList();

    public static Team Create(string name, AssignmentStrategy strategy, IEnumerable<Guid> memberIds)
    {
        var team = new Team();
        team.Update(name, strategy, memberIds);
        return team;
    }

    public void Update(string name, AssignmentStrategy strategy, IEnumerable<Guid> memberIds)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length is 0 or > NameMaxLength)
            throw new DomainException("team.invalid_name", $"Team name must be 1 to {NameMaxLength} characters.");
        var ids = (memberIds ?? []).Distinct().ToList();
        if (ids.Count > MaxMembers)
            throw new DomainException("team.too_many_members", $"A team has at most {MaxMembers} members.");

        Name = name;
        Strategy = strategy;

        // Keep existing members' order; newcomers join at the end.
        _members.RemoveAll(m => !ids.Contains(m.UserId));
        var next = _members.Count == 0 ? 0 : _members.Max(m => m.JoinedOrder) + 1;
        foreach (var id in ids.Where(id => _members.All(m => m.UserId != id)))
            _members.Add(new TeamMember(id, next++));

        if (LastAssignedUserId is { } last && !ids.Contains(last))
            LastAssignedUserId = null;
    }

    public bool HasMember(Guid userId) => _members.Any(m => m.UserId == userId);

    public void SetConfidentialGroup(bool isConfidentialGroup) => IsConfidentialGroup = isConfidentialGroup;

    /// <summary>
    /// Applies the team's strategy among <paramref name="eligible"/> members (e.g. active ones) and moves the
    /// round-robin pointer when someone is picked.
    /// </summary>
    public Guid? PickAssignee(IReadOnlyCollection<Guid> eligible, IReadOnlyDictionary<Guid, int> activeLoad)
    {
        var members = OrderedMemberIds.Where(eligible.Contains).ToList();
        var picked = AssignmentPickers.For(Strategy).Pick(new AssignmentContext(members, activeLoad, LastAssignedUserId));
        if (picked is not null)
            LastAssignedUserId = picked;
        return picked;
    }
}

/// <summary>Membership of a user in a team.</summary>
public sealed record TeamMember(Guid UserId, int JoinedOrder);
