using System.Security.Cryptography;
using System.Text;
using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Users;

namespace HrServiceDesk.Domain.Tests.Users;

public class SsoTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    private static SsoConfiguration Configuration(params string[] domains)
    {
        var configuration = SsoConfiguration.Create();
        configuration.Update(true, "Microsoft", "https://login.microsoftonline.com/tid/v2.0/", null, "client", domains, false, false, allowInsecureUrls: false);
        return configuration;
    }

    [Fact]
    public void Domains_are_normalised_and_matched_case_insensitively()
    {
        var configuration = Configuration(" @Acme.COM ", "acme.tn", "acme.com");

        configuration.EmailDomains.Should().Equal("acme.com", "acme.tn");
        configuration.HandlesEmail("Amira@ACME.com").Should().BeTrue();
        configuration.HandlesEmail("someone@evil-acme.com").Should().BeFalse();
        configuration.HandlesEmail("no-at-sign").Should().BeFalse();
        configuration.EffectiveMetadataAddress.Should().Be("https://login.microsoftonline.com/tid/v2.0/.well-known/openid-configuration");
    }

    [Theory]
    [InlineData("http://login.example/tid")]
    [InlineData("not a url")]
    public void The_authority_must_be_https(string authority)
    {
        var act = () => SsoConfiguration.Create().Update(true, "Microsoft", authority, null, "client", ["acme.com"], false, false, allowInsecureUrls: false);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("sso.invalid_authority");
    }

    [Fact]
    public void At_least_one_valid_domain_is_needed()
    {
        var act = () => Configuration("localhost");

        act.Should().Throw<DomainException>().Which.Code.Should().Be("sso.invalid_domains");
    }

    [Fact]
    public void An_attempt_is_single_use_and_expires_after_ten_minutes()
    {
        var attempt = SsoLoginAttempt.Start(Guid.NewGuid(), "/portal", rememberMe: false, Now);

        attempt.TryUse(Now.AddMinutes(9)).Should().BeTrue();
        attempt.TryUse(Now.AddMinutes(9)).Should().BeFalse();
        SsoLoginAttempt.Start(Guid.NewGuid(), "/", false, Now).TryUse(Now.AddMinutes(11)).Should().BeFalse();
    }

    [Fact]
    public void The_pkce_challenge_is_the_sha256_of_the_verifier()
    {
        var attempt = SsoLoginAttempt.Start(Guid.NewGuid(), "/", false, Now);
        var expected = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(attempt.CodeVerifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        attempt.CodeChallenge.Should().Be(expected);
        attempt.State.Should().NotBe(attempt.Nonce);
    }

    [Theory]
    [InlineData("/portal/requests", "/portal/requests")]
    [InlineData("https://evil.example", "/")]
    [InlineData("//evil.example", "/")]
    [InlineData("/\\evil.example", "/")]
    [InlineData(null, "/")]
    public void Only_app_paths_are_kept_as_return_url(string? returnUrl, string expected) =>
        SsoLoginAttempt.SafeReturnUrl(returnUrl).Should().Be(expected);
}
