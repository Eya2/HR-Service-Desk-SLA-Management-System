using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using HrServiceDesk.Application.Escalations;
using HrServiceDesk.Application.Integration;
using HrServiceDesk.IntegrationTests.Infrastructure;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace HrServiceDesk.IntegrationTests.Demo;

/// <summary>
/// The end-to-end demo story (docs/DEMO.md), run with a test clock so days pass in seconds:
/// payslip correction → manager approval → payroll webhook → SLA deadline that skips a public holiday →
/// at risk → breached → escalation → dashboard; sensitive access in the audit log; a confidential case
/// hidden from an HR officer; payroll resolves through the integration API; the employee closes and rates.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DemoScenarioTests(PostgresFixture postgres)
{
    private static readonly TimeZoneInfo Tunis = TimeZoneInfo.FindSystemTimeZoneById("Africa/Tunis");

    /// <summary>Wednesday 14 October 2026, 14:00 in Tunis; Thursday 15 October is Evacuation Day (public holiday).</summary>
    private static DateTimeOffset Tn(int day, int hour, int minute = 0) => new(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(1));

    private sealed record Sla(string State, DateTimeOffset? FirstResponseDueAt, DateTimeOffset? ResolutionDueAt, bool ResolutionBreached);

    private sealed record Person(Guid Id, string FullName);

    private sealed record Comment(string AuthorName, string Body);

    private sealed record Event(string Type, string? ActorName);

    private sealed record Approval(Guid Id, string Decision);

    private sealed record Details(Guid Id, string Reference, string Status, string Priority, Person? Assignee, Sla Sla, Comment[] Comments, Event[] Timeline, Approval[] Approvals);

    private sealed record Pending(Guid ApprovalId, Guid TicketId);

    private sealed record Kpis(int BreachedNow);

    private sealed record Board(Kpis Kpis);

    private sealed record AuditEntry(string Action, string Summary, string UserName);

    private sealed record AuditPage(AuditEntry[] Items);

    private sealed record CreatedKey(string Secret);

    private sealed record SavedWebhook(JsonElement Webhook, string Secret);

    [Fact]
    public async Task The_demo_story_runs_from_request_to_rating()
    {
        await using var api = new ClockedApiFactory(postgres.ConnectionString, Tn(14, 14));
        var employee = await SignedInAs(api, DemoUsers.AcmeEmployee);
        var manager = await SignedInAs(api, DemoUsers.AcmeManager);
        var admin = await SignedInAs(api, DemoUsers.AcmeHrAdmin);
        var officer = await SignedInAs(api, DemoUsers.AcmeHrOfficer);

        // The payroll system is connected: a key to call the desk, a webhook to hear about payroll cases.
        var key = (await (await admin.PostAsJsonAsync("/api/integrations/api-keys",
            new { name = "Demo payroll", scopes = new[] { "tickets:read", "tickets:write" } })).Content.ReadFromJsonAsync<CreatedKey>())!.Secret;
        var receiver = $"https://payroll.example/{Guid.NewGuid():N}";
        var webhook = (await (await admin.PostAsJsonAsync("/api/integrations/webhooks",
            new { name = "Demo payroll", url = receiver, events = new[] { "ticket.status_changed" }, isActive = true, rotateSecret = false }))
            .Content.ReadFromJsonAsync<SavedWebhook>())!;
        var payroll = api.CreateApiClient();
        payroll.DefaultRequestHeaders.Add("X-Api-Key", key);

        try
        {
            // 1. Wednesday 14:00 — Amira asks for a payslip correction; it waits for her manager (SLA paused).
            var ticket = await employee.SubmitPayslipCorrectionAsync("September payslip: overtime missing");
            (await Get(employee, ticket.Id)).Status.Should().Be("PendingApproval");

            // 2. 15:00 — Youssef approves from his queue; the case goes to the Payroll team.
            api.Clock.Now = Tn(14, 15);
            var pending = await manager.GetFromJsonAsync<Pending[]>("/api/approvals/pending");
            var approval = pending!.Single(p => p.TicketId == ticket.Id);
            (await manager.PostAsJsonAsync($"/api/tickets/{ticket.Id}/approvals/{approval.ApprovalId}/decision", new { approve = true, comment = (string?)null }))
                .EnsureSuccessStatusCode();
            var approved = await Get(admin, ticket.Id);
            approved.Status.Should().Be("Open");
            approved.Assignee.Should().NotBeNull();

            // 3. The deadline skips Thursday 15 October (public holiday): 2 business hours on Wednesday + 2 on Friday morning.
            TimeZoneInfo.ConvertTime(approved.Sla.ResolutionDueAt!.Value, Tunis).Should().Be(Tn(16, 10));

            // 4. The payroll system is told, with a signed, thin event.
            await Run(api, new DispatchWebhooksCommand());
            var sent = api.Webhooks.Sent.Where(r => r.Url == receiver).Last();
            sent.Secret.Should().Be(webhook.Secret);
            using (var payload = JsonDocument.Parse(sent.Payload))
            {
                payload.RootElement.GetProperty("data").GetProperty("change").GetProperty("to").GetString().Should().Be("Open");
                payload.RootElement.GetProperty("data").GetProperty("ticket").GetProperty("requestType").GetProperty("category").GetString().Should().Be("Payroll");
            }

            // 5. 15:30 — payroll acknowledges through the integration API (first response in time).
            api.Clock.Now = Tn(14, 15, 30);
            (await payroll.PostAsJsonAsync($"/api/integration/v1/tickets/{ticket.Id}/comments", new { body = "Received by payroll, checking the timesheets." }))
                .EnsureSuccessStatusCode();

            // 6. Friday 09:20 — 200 of 240 business minutes used: at risk, the assignee is warned.
            api.Clock.Now = Tn(16, 9, 20);
            await Run(api, new RunSlaMonitorCommand());
            (await Get(admin, ticket.Id)).Sla.State.Should().Be("AtRisk");

            // 7. Friday 10:30 — past the deadline: breached; the manager is alerted and the priority raised.
            api.Clock.Now = Tn(16, 10, 30);
            await Run(api, new RunSlaMonitorCommand());
            var breached = await Get(admin, ticket.Id);
            breached.Sla.State.Should().Be("Breached");
            breached.Priority.Should().Be("Critical");
            breached.Timeline.Should().Contain(e => e.Type == "Escalated");

            // 8. The dashboard shows the late case.
            var period = $"from={Uri.EscapeDataString(Tn(14, 0).ToString("o"))}&to={Uri.EscapeDataString(Tn(17, 0).ToString("o"))}";
            (await admin.GetFromJsonAsync<Board>($"/api/dashboard?{period}"))!.Kpis.BreachedNow.Should().BeGreaterThan(0);

            // 9. Sensitive data: an HR officer opens a bank-details change, and the access is in the audit log.
            var bankType = await employee.RequestTypeIdAsync("Change of bank details");
            var bank = await employee.SubmitAsync(bankType, "New bank account", new JsonObject { ["bankName"] = "BIAT", ["iban"] = "TN5910006035183598478831" },
                new TestFile("bankCertificate", "rib.pdf", TicketApi.Pdf));
            var bankId = (await bank.Content.ReadFromJsonAsync<Created>())!.Id;
            (await officer.GetAsync($"/api/tickets/{bankId}")).EnsureSuccessStatusCode();
            var audit = await admin.GetFromJsonAsync<AuditPage>("/api/audit-logs?action=SensitiveCaseViewed&pageSize=100");
            audit!.Items.Should().Contain(a => a.UserName == "Leila Mansour");

            // 10. A confidential report: the restricted group (HR Admin) sees it, the HR officer does not.
            var reportType = await employee.RequestTypeIdAsync("Harassment report");
            var report = await employee.SubmitAsync(reportType, "Report", new JsonObject
            {
                ["incidentDate"] = "2026-10-12",
                ["whatHappened"] = "Repeated inappropriate remarks during meetings.",
            });
            var reportId = (await report.Content.ReadFromJsonAsync<Created>())!.Id;
            (await officer.GetAsync($"/api/tickets/{reportId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await admin.GetAsync($"/api/tickets/{reportId}")).StatusCode.Should().Be(HttpStatusCode.OK);

            // 11. 11:00 — payroll resolves the correction; Amira reads the answer, closes and rates.
            api.Clock.Now = Tn(16, 11);
            (await payroll.PostAsJsonAsync($"/api/integration/v1/tickets/{ticket.Id}/status", new { status = "InProgress" })).EnsureSuccessStatusCode();
            (await payroll.PostAsJsonAsync($"/api/integration/v1/tickets/{ticket.Id}/status",
                new { status = "Resolved", reason = "Booked as PAY-202610-0042, paid with the October salary." })).EnsureSuccessStatusCode();
            var resolved = await Get(employee, ticket.Id);
            resolved.Status.Should().Be("Resolved");
            resolved.Comments.Should().Contain(c => c.AuthorName == "Demo payroll (API)" && c.Body.Contains("PAY-202610-0042", StringComparison.Ordinal));

            api.Clock.Now = Tn(16, 14);
            (await employee.ChangeStatusAsync(ticket.Id, "Closed")).EnsureSuccessStatusCode();
            (await employee.PostAsJsonAsync($"/api/tickets/{ticket.Id}/satisfaction", new { score = 4, comment = "Late, but well handled." }))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally
        {
            await admin.DeleteAsync($"/api/integrations/webhooks/{webhook.Webhook.GetProperty("id").GetGuid()}");
        }
    }

    private static async Task<HttpClient> SignedInAs(ApiFactory api, string email)
    {
        var client = api.CreateApiClient();
        return client.Authorize(await client.LoginAsync(email));
    }

    private static async Task<Details> Get(HttpClient client, Guid id) => (await client.GetFromJsonAsync<Details>($"/api/tickets/{id}"))!;

    private static async Task Run<T>(ApiFactory api, IRequest<T> command)
    {
        using var scope = api.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
    }
}
