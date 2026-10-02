using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Integration;

namespace HrServiceDesk.Domain.Tests.Integration;

public class ApiKeyTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 4, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_issued_key_matches_its_secret_only_and_stores_no_secret()
    {
        var (key, secret) = ApiKey.Issue("Payroll", [ApiScopes.TicketsRead], Guid.NewGuid(), Guid.NewGuid());

        secret.Should().StartWith($"hrd_{key.Prefix}_");
        key.Matches(secret).Should().BeTrue();
        key.Matches(secret + "x").Should().BeFalse();
        key.KeyHash.Should().NotContain(secret).And.HaveLength(64);
        ApiKey.PrefixOf(secret).Should().Be(key.Prefix);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer abc")]
    [InlineData("hrd_short_xxxxxxxxxxxxxxxxxxxx")]
    [InlineData("hrd_abcdefgh_tooshort")]
    public void Malformed_keys_have_no_prefix(string? presented) => ApiKey.PrefixOf(presented).Should().BeNull();

    [Fact]
    public void Scopes_must_be_known()
    {
        var act = () => ApiKey.Issue("Payroll", ["tickets:delete"], Guid.NewGuid(), null);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("api_key.invalid_scopes");
    }

    [Fact]
    public void Use_is_recorded_at_most_once_a_minute_and_revocation_sticks()
    {
        var (key, _) = ApiKey.Issue("Payroll", ApiScopes.All, Guid.NewGuid(), null);

        key.MarkUsed(Now).Should().BeTrue();
        key.MarkUsed(Now.AddSeconds(30)).Should().BeFalse();
        key.MarkUsed(Now.AddMinutes(2)).Should().BeTrue();

        key.Revoke(Now);
        key.Revoke(Now.AddDays(1));
        key.IsActive.Should().BeFalse();
        key.RevokedAt.Should().Be(Now);
    }
}
