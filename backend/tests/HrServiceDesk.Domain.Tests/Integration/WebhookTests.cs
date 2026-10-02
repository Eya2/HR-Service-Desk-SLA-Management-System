using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Integration;

namespace HrServiceDesk.Domain.Tests.Integration;

public class WebhookTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 4, 9, 0, 0, TimeSpan.Zero);

    private static WebhookSubscription Subscription() =>
        WebhookSubscription.Create("Payroll", "https://payroll.example/hooks", [WebhookEvents.TicketCreated], "v1:secret", allowInsecureUrls: false);

    [Theory]
    [InlineData("http://payroll.example/hooks")]
    [InlineData("ftp://payroll.example")]
    [InlineData("https://user:pass@payroll.example")]
    [InlineData("/relative")]
    public void Urls_must_be_https_without_credentials(string url)
    {
        var act = () => WebhookSubscription.ValidateUrl(url, allowInsecure: false);

        act.Should().Throw<DomainException>().Which.Code.Should().Be("webhook.invalid_url");
    }

    [Fact]
    public void Plain_http_is_accepted_only_where_allowed() =>
        WebhookSubscription.ValidateUrl("http://mock-payroll:8080/webhooks", allowInsecure: true).Should().Be("http://mock-payroll:8080/webhooks");

    [Fact]
    public void A_subscription_receives_only_its_events_while_active()
    {
        var subscription = Subscription();

        subscription.Wants(WebhookEvents.TicketCreated).Should().BeTrue();
        subscription.Wants(WebhookEvents.TicketStatusChanged).Should().BeFalse();
        subscription.Update("Payroll", subscription.Url, [WebhookEvents.TicketCreated], isActive: false, allowInsecureUrls: false);
        subscription.Wants(WebhookEvents.TicketCreated).Should().BeFalse();
    }

    [Fact]
    public void Failures_are_retried_after_1_5_30_120_and_360_minutes_then_given_up()
    {
        var delivery = WebhookDelivery.Create(Guid.NewGuid(), Subscription(), WebhookEvents.TicketCreated, "{}", null, Now);
        var at = Now;
        var expectedDelays = new[] { 1, 5, 30, 120, 360 };

        foreach (var minutes in expectedDelays)
        {
            delivery.RecordFailure(500, "HTTP 500", at);
            delivery.NextAttemptAt.Should().Be(at.AddMinutes(minutes));
            delivery.IsDue(at.AddMinutes(minutes).AddSeconds(-1)).Should().BeFalse();
            at = at.AddMinutes(minutes);
        }

        delivery.RecordFailure(500, "HTTP 500", at);
        delivery.Status.Should().Be(WebhookDeliveryStatus.Failed);
        delivery.Attempts.Should().Be(WebhookDelivery.MaxAttempts);
        delivery.IsDue(at.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void A_failed_delivery_can_be_sent_again_from_the_start()
    {
        var delivery = WebhookDelivery.Create(Guid.NewGuid(), Subscription(), WebhookEvents.TicketCreated, "{}", null, Now);
        for (var i = 0; i < WebhookDelivery.MaxAttempts; i++)
            delivery.RecordFailure(null, new string('x', 900), Now);
        delivery.LastError.Should().HaveLength(WebhookDelivery.ErrorMaxLength);

        delivery.Redeliver(Now.AddHours(1));

        delivery.Status.Should().Be(WebhookDeliveryStatus.Pending);
        delivery.Attempts.Should().Be(0);
        delivery.IsDue(Now.AddHours(1)).Should().BeTrue();
        var again = () => delivery.Redeliver(Now);
        again.Should().Throw<DomainException>();
    }

    [Fact]
    public void Success_is_recorded_once()
    {
        var delivery = WebhookDelivery.Create(Guid.NewGuid(), Subscription(), WebhookEvents.TicketCreated, "{}", null, Now);

        delivery.RecordSuccess(204, Now);

        delivery.Status.Should().Be(WebhookDeliveryStatus.Succeeded);
        delivery.DeliveredAt.Should().Be(Now);
        delivery.IsDue(Now.AddHours(1)).Should().BeFalse();
    }
}
