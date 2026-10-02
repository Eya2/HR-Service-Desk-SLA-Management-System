using System.Net;
using HrServiceDesk.Infrastructure.Integration;
using Microsoft.Extensions.Options;

namespace HrServiceDesk.IntegrationTests.Integration;

/// <summary>Infrastructure pieces that need no database: SSRF address filter and secret encryption.</summary>
public sealed class WebhookSecurityTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.20.0.5")]
    [InlineData("192.168.1.10")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("::1")]
    [InlineData("fd00::1")]
    [InlineData("fe80::1")]
    [InlineData("::ffff:10.0.0.1")]
    public void Private_and_reserved_addresses_are_refused(string address) =>
        HttpWebhookSender.IsPublic(IPAddress.Parse(address)).Should().BeFalse();

    [Theory]
    [InlineData("93.184.216.34")]
    [InlineData("172.32.0.1")]
    [InlineData("2606:2800:220:1:248:1893:25c8:1946")]
    public void Public_addresses_are_allowed(string address) => HttpWebhookSender.IsPublic(IPAddress.Parse(address)).Should().BeTrue();

    [Fact]
    public void Secrets_are_encrypted_at_rest_and_read_back()
    {
        var protector = new AesSecretProtector(Options.Create(new IntegrationOptions { SecretKey = "a-long-enough-test-key" }));

        var stored = protector.Protect("whsec_abc");

        stored.Should().StartWith("v1:").And.NotContain("whsec_abc");
        protector.Protect("whsec_abc").Should().NotBe(stored, "each encryption uses a new nonce");
        protector.Unprotect(stored).Should().Be("whsec_abc");
        var otherKey = new AesSecretProtector(Options.Create(new IntegrationOptions { SecretKey = "another-long-test-key" }));
        var act = () => otherKey.Unprotect(stored);
        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }
}
