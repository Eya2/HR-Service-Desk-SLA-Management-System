using HrServiceDesk.Application.Integration;

namespace HrServiceDesk.Application.Tests.Integration;

public class WebhookSignatureTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_780_000_000);
    private const string Body = """{"id":"1","type":"ping"}""";

    [Fact]
    public void Matches_a_known_hmac_sha256_vector()
    {
        // Same as: printf '1780000000.{"id":"1","type":"ping"}' | openssl dgst -sha256 -hmac whsec_test
        WebhookSignature.Compute("whsec_test", 1_780_000_000, Body).Should().Be("t=1780000000,v1=98c3f375477ecb6525531be03287eaeea4bf4f27363d01bb70b3b285273d5080");
    }

    [Fact]
    public void A_receiver_accepts_the_genuine_signature_and_rejects_tampering_and_replays()
    {
        var header = WebhookSignature.Compute("whsec_test", Now.ToUnixTimeSeconds(), Body);

        WebhookSignature.Verify("whsec_test", header, Body, Now.AddSeconds(30), TimeSpan.FromMinutes(5)).Should().BeTrue();
        WebhookSignature.Verify("whsec_other", header, Body, Now, TimeSpan.FromMinutes(5)).Should().BeFalse();
        WebhookSignature.Verify("whsec_test", header, Body.Replace("ping", "pong", StringComparison.Ordinal), Now, TimeSpan.FromMinutes(5)).Should().BeFalse();
        WebhookSignature.Verify("whsec_test", header, Body, Now.AddMinutes(10), TimeSpan.FromMinutes(5)).Should().BeFalse();
        WebhookSignature.Verify("whsec_test", "garbage", Body, Now, TimeSpan.FromMinutes(5)).Should().BeFalse();
        WebhookSignature.Verify("whsec_test", null, Body, Now, TimeSpan.FromMinutes(5)).Should().BeFalse();
    }

    [Fact]
    public void New_secrets_are_random() => WebhookSignature.NewSecret().Should().StartWith("whsec_").And.NotBe(WebhookSignature.NewSecret());
}
