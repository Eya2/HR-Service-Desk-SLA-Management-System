using HrServiceDesk.Domain.Users;

namespace HrServiceDesk.Domain.Tests.Users;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly User Owner = User.Create("a@acme.example", "A", "B", [Role.Employee]);

    [Fact]
    public void Issue_starts_a_new_family_by_default()
    {
        var token = RefreshToken.Issue(Owner, "hash", Now, TimeSpan.FromDays(7));

        token.UserId.Should().Be(Owner.Id);
        token.FamilyId.Should().NotBeEmpty();
        token.ExpiresAt.Should().Be(Now.AddDays(7));
        token.IsActive(Now).Should().BeTrue();
    }

    [Fact]
    public void Issue_can_continue_an_existing_family()
    {
        var first = RefreshToken.Issue(Owner, "h1", Now, TimeSpan.FromDays(7));

        var second = RefreshToken.Issue(Owner, "h2", Now, TimeSpan.FromDays(7), first.FamilyId);

        second.FamilyId.Should().Be(first.FamilyId);
    }

    [Fact]
    public void Token_is_inactive_once_expired()
    {
        var token = RefreshToken.Issue(Owner, "hash", Now, TimeSpan.FromDays(7));

        token.IsActive(Now.AddDays(7)).Should().BeFalse();
        token.IsExpired(Now.AddDays(7)).Should().BeTrue();
    }

    [Fact]
    public void Revoke_records_successor_and_is_idempotent()
    {
        var token = RefreshToken.Issue(Owner, "hash", Now, TimeSpan.FromDays(7));
        var successor = Guid.NewGuid();

        token.Revoke(Now, successor);
        token.Revoke(Now.AddHours(1), Guid.NewGuid());

        token.IsActive(Now).Should().BeFalse();
        token.RevokedAt.Should().Be(Now);
        token.ReplacedByTokenId.Should().Be(successor);
    }
}
