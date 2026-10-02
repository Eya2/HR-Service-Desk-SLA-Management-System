using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HrServiceDesk.Application.Integration;
using HrServiceDesk.IntegrationTests.Infrastructure;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace HrServiceDesk.IntegrationTests.Integration;

[Collection(PostgresCollection.Name)]
public sealed class WebhookTests(PostgresFixture postgres)
{
    private sealed record Webhook(Guid Id, string Name, string Url, string[] Events, bool IsActive, int PendingDeliveries, int FailedDeliveries);

    private sealed record Saved(Webhook Webhook, string? Secret);

    private sealed record Delivery(Guid Id, string EventType, Guid? TicketId, string Status, int Attempts, int? LastStatusCode, string? LastError);

    private sealed record Deliveries(Delivery[] Items, int TotalCount);

    private static readonly DateTimeOffset Start = new(2026, 5, 4, 9, 0, 0, TimeSpan.FromHours(1));

    private static async Task<HttpClient> SignedInAs(ApiFactory api, string email)
    {
        var client = api.CreateApiClient();
        return client.Authorize(await client.LoginAsync(email));
    }

    private static async Task DispatchAsync(ApiFactory api)
    {
        using var scope = api.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new DispatchWebhooksCommand());
    }

    /// <summary>Each test uses its own URL, so deliveries of other tests' subscriptions are told apart; the subscription is removed after.</summary>
    private static async Task<Saved> SubscribeAsync(HttpClient admin, string url, params string[] events)
    {
        var response = await admin.PostAsJsonAsync("/api/integrations/webhooks", new { name = "Test receiver", url, events, isActive = true, rotateSecret = false });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Saved>())!;
    }

    [Fact]
    public async Task Case_events_are_sent_signed_with_a_thin_payload()
    {
        await using var api = new ClockedApiFactory(postgres.ConnectionString, Start);
        var admin = await SignedInAs(api, DemoUsers.AcmeHrAdmin);
        var url = $"https://receiver.example/{Guid.NewGuid():N}";
        var saved = await SubscribeAsync(admin, url, "ticket.created", "ticket.status_changed");
        saved.Secret.Should().StartWith("whsec_");
        try
        {
            var employee = await SignedInAs(api, DemoUsers.AcmeEmployee);
            var ticket = await employee.SubmitWorkCertificateAsync("Webhook payload");
            (await admin.ChangeStatusAsync(ticket.Id, "Open")).EnsureSuccessStatusCode();

            await DispatchAsync(api);

            var sent = api.Webhooks.Sent.Where(r => r.Url == url).ToList();
            sent.Select(r => r.EventType).Should().Equal("ticket.created", "ticket.status_changed");
            sent.Should().OnlyContain(r => r.Secret == saved.Secret);

            using var payload = JsonDocument.Parse(sent[1].Payload);
            var root = payload.RootElement;
            root.GetProperty("id").GetGuid().Should().Be(sent[1].DeliveryId);
            root.GetProperty("data").GetProperty("ticket").GetProperty("reference").GetString().Should().Be(ticket.Reference);
            root.GetProperty("data").GetProperty("ticket").GetProperty("requestType").GetProperty("name").GetString().Should().Be("Work certificate");
            root.GetProperty("data").GetProperty("change").GetProperty("to").GetString().Should().Be("Open");
            sent[1].Payload.Should().NotContain("Webhook payload", "titles and free text are not sent");

            var deliveries = await admin.GetFromJsonAsync<Deliveries>($"/api/integrations/webhooks/{saved.Webhook.Id}/deliveries");
            deliveries!.Items.Should().HaveCount(2).And.OnlyContain(d => d.Status == "Succeeded" && d.Attempts == 1);
        }
        finally
        {
            await admin.DeleteAsync($"/api/integrations/webhooks/{saved.Webhook.Id}");
        }
    }

    [Fact]
    public async Task Failed_deliveries_are_retried_with_growing_delays_then_given_up_and_can_be_sent_again()
    {
        await using var api = new ClockedApiFactory(postgres.ConnectionString, Start);
        var admin = await SignedInAs(api, DemoUsers.AcmeHrAdmin);
        var url = $"https://down.example/{Guid.NewGuid():N}";
        var saved = await SubscribeAsync(admin, url, "ticket.created");
        api.Webhooks.Respond = r => r.Url == url ? new(false, 503, "HTTP 503 Service Unavailable") : new(true, 200, null);
        try
        {
            var employee = await SignedInAs(api, DemoUsers.AcmeEmployee);
            await employee.SubmitWorkCertificateAsync("Receiver down");

            await DispatchAsync(api);
            await DispatchAsync(api); // nothing due yet: the retry waits a minute
            api.Webhooks.Sent.Count(r => r.Url == url).Should().Be(1);

            foreach (var delay in new[] { 1, 5, 30, 120, 360 })
            {
                api.Clock.Advance(TimeSpan.FromMinutes(delay));
                await DispatchAsync(api);
            }

            api.Webhooks.Sent.Count(r => r.Url == url).Should().Be(6);
            var delivery = (await admin.GetFromJsonAsync<Deliveries>($"/api/integrations/webhooks/{saved.Webhook.Id}/deliveries"))!.Items.Single();
            delivery.Status.Should().Be("Failed");
            delivery.Attempts.Should().Be(6);
            delivery.LastStatusCode.Should().Be(503);

            api.Webhooks.Respond = _ => new(true, 204, null);
            (await admin.PostAsync($"/api/integrations/deliveries/{delivery.Id}/redeliver", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
            await DispatchAsync(api);
            (await admin.GetFromJsonAsync<Deliveries>($"/api/integrations/webhooks/{saved.Webhook.Id}/deliveries"))!.Items.Single().Status.Should().Be("Succeeded");
        }
        finally
        {
            await admin.DeleteAsync($"/api/integrations/webhooks/{saved.Webhook.Id}");
        }
    }

    [Fact]
    public async Task Confidential_cases_and_internal_notes_are_never_sent_and_pings_check_the_receiver()
    {
        await using var api = new ClockedApiFactory(postgres.ConnectionString, Start);
        var admin = await SignedInAs(api, DemoUsers.AcmeHrAdmin);
        var url = $"https://quiet.example/{Guid.NewGuid():N}";
        var saved = await SubscribeAsync(admin, url, "ticket.created", "ticket.comment_added");
        try
        {
            var employee = await SignedInAs(api, DemoUsers.AcmeEmployee);
            var typeId = await employee.RequestTypeIdAsync("Harassment report");
            (await employee.SubmitAsync(typeId, "Confidential",
                new System.Text.Json.Nodes.JsonObject { ["incidentDate"] = "2026-03-01", ["whatHappened"] = "A detailed description of what happened." }))
                .EnsureSuccessStatusCode();
            var ticket = await employee.SubmitWorkCertificateAsync("Internal note");
            (await admin.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new { body = "HR only", isInternal = true })).EnsureSuccessStatusCode();
            (await admin.PostAsync($"/api/integrations/webhooks/{saved.Webhook.Id}/ping", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

            await DispatchAsync(api);

            api.Webhooks.Sent.Where(r => r.Url == url).Select(r => r.EventType).Should().BeEquivalentTo(["ticket.created", "ping"]);
        }
        finally
        {
            await admin.DeleteAsync($"/api/integrations/webhooks/{saved.Webhook.Id}");
        }
    }

    [Fact]
    public async Task Only_hr_admins_manage_webhooks_and_urls_are_validated()
    {
        var employee = await SignedInAs(postgres.Api, DemoUsers.AcmeEmployee);
        (await employee.GetAsync("/api/integrations/webhooks")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await SignedInAs(postgres.Api, DemoUsers.AcmeHrAdmin);
        (await admin.PostAsJsonAsync("/api/integrations/webhooks", new { name = "Bad", url = "ftp://x.example", events = new[] { "ticket.created" }, isActive = true, rotateSecret = false }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await admin.PostAsJsonAsync("/api/integrations/webhooks", new { name = "Bad", url = "https://x.example", events = new[] { "nope" }, isActive = true, rotateSecret = false }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
