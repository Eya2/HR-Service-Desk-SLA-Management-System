using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using HrServiceDesk.Application.Escalations;
using HrServiceDesk.IntegrationTests.Infrastructure;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace HrServiceDesk.IntegrationTests.Notifications;

[Collection(PostgresCollection.Name)]
public sealed class NotificationAndEscalationTests(PostgresFixture postgres)
{
    private const string Sami = "sami.gharbi@acme.example";

    private sealed record NotificationItem(Guid Id, string Type, string Title, string Message, Guid? TicketId, bool IsRead);

    private sealed record NotificationPage(NotificationItem[] Items, int TotalCount);

    private sealed record CaseView(Guid Id, string Priority, string Status, SlaView Sla, TimelineEntry[] Timeline);

    private sealed record SlaView(string State);

    private sealed record Rule(Guid Id, string Name, string Trigger, string Action, int TimesFired);

    /// <summary>Tunis local time (UTC+1).</summary>
    private static DateTimeOffset Tn(int month, int day, int hour, int minute = 0) => new(2026, month, day, hour, minute, 0, TimeSpan.FromHours(1));

    private static async Task<HttpClient> SignedInAs(ApiFactory api, string email)
    {
        var client = api.CreateApiClient();
        return client.Authorize(await client.LoginAsync(email));
    }

    private static async Task<NotificationItem[]> NotificationsAsync(HttpClient client, Guid ticketId) =>
        (await client.GetFromJsonAsync<NotificationPage>("/api/notifications?pageSize=100"))!.Items.Where(n => n.TicketId == ticketId).ToArray();

    private static async Task<SlaMonitorReport> RunMonitorAsync(ApiFactory api)
    {
        using var scope = api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunSlaMonitorCommand());
    }

    [Fact]
    public async Task People_involved_in_a_case_are_notified_in_app_and_by_email()
    {
        await using var api = new ClockedApiFactory(postgres.ConnectionString, Tn(3, 16, 9));
        var employee = await SignedInAs(api, DemoUsers.AcmeEmployee);
        var created = await employee.SubmitWorkCertificateAsync("Notified certificate");
        var assigneeName = (await employee.GetFromJsonAsync<TicketDetails>($"/api/tickets/{created.Id}"))!.Assignee!.FullName;
        var assignee = await SignedInAs(api, assigneeName == "Leila Mansour" ? DemoUsers.AcmeHrOfficer : DemoUsers.AcmeHrAdmin);

        (await NotificationsAsync(employee, created.Id)).Select(n => n.Type).Should().Equal("TicketCreated");
        (await NotificationsAsync(assignee, created.Id)).Select(n => n.Type).Should().Contain("TicketAssigned");

        await assignee.PostAsJsonAsync($"/api/tickets/{created.Id}/comments", new { body = "We are preparing it.", isInternal = false });
        await assignee.PostAsJsonAsync($"/api/tickets/{created.Id}/comments", new { body = "HR only", isInternal = true });

        var employeeNotes = await NotificationsAsync(employee, created.Id);
        employeeNotes.Count(n => n.Type == "CommentAdded").Should().Be(1, "internal notes are not announced to the employee");
        (await NotificationsAsync(assignee, created.Id)).Should().NotContain(n => n.Type == "CommentAdded", "nobody is notified of their own message");

        var unread = await employee.GetFromJsonAsync<int>("/api/notifications/unread-count");
        unread.Should().BeGreaterThanOrEqualTo(2);
        (await employee.PostAsync($"/api/notifications/{employeeNotes[0].Id}/read", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await employee.GetFromJsonAsync<int>("/api/notifications/unread-count")).Should().Be(unread - 1);
        await employee.PostAsync("/api/notifications/read-all", null);
        (await employee.GetFromJsonAsync<int>("/api/notifications/unread-count")).Should().Be(0);

        api.Emails.Sent.Should().Contain(m => m.To == DemoUsers.AcmeEmployee && m.Subject.Contains(created.Reference, StringComparison.Ordinal)
            && m.Body.Contains($"/tickets/{created.Id}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Managers_are_asked_to_approve_and_confidential_emails_hide_the_title()
    {
        await using var api = new ClockedApiFactory(postgres.ConnectionString, Tn(3, 16, 9));
        var employee = await SignedInAs(api, DemoUsers.AcmeEmployee);
        var manager = await SignedInAs(api, DemoUsers.AcmeManager);

        var payslip = await employee.SubmitPayslipCorrectionAsync("Approval needed");
        (await NotificationsAsync(manager, payslip.Id)).Should().ContainSingle(n => n.Type == "ApprovalRequested");

        var typeId = await employee.RequestTypeIdAsync("Harassment report");
        await employee.SubmitAsync(typeId, "Secret title about my colleague", new JsonObject
        {
            ["incidentDate"] = "2026-03-13", ["whatHappened"] = "Something that must stay confidential.",
        });

        api.Emails.Sent.Should().NotContain(m => m.Subject.Contains("Secret title", StringComparison.Ordinal) || m.Body.Contains("Secret title", StringComparison.Ordinal));
        api.Emails.Sent.Should().Contain(m => m.To == DemoUsers.AcmeEmployee && m.Body.Contains("(confidential case)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_monitor_moves_a_case_to_at_risk_then_breached_and_escalates_once()
    {
        // Change of bank details: Payroll policy, High priority (first response 60, resolution 240 business minutes),
        // handled by the Payroll team (Sami). Submitted Thursday 15:30; Friday 20 March is a holiday.
        await using var api = new ClockedApiFactory(postgres.ConnectionString, Tn(3, 19, 15, 30));
        var employee = await SignedInAs(api, DemoUsers.AcmeEmployee);
        var payroll = await SignedInAs(api, Sami);
        var admin = await SignedInAs(api, DemoUsers.AcmeHrAdmin);
        var typeId = await employee.RequestTypeIdAsync("Change of bank details");
        var response = await employee.SubmitAsync(typeId, "New bank account", new JsonObject { ["bankName"] = "BIAT", ["iban"] = "TN5910006035183598478831" },
            new TestFile("bankCertificate", "rib.pdf", TicketApi.Pdf));
        var id = (await response.Content.ReadFromJsonAsync<Created>())!.Id;

        api.Clock.Now = Tn(3, 19, 15, 40);
        await payroll.PostAsJsonAsync($"/api/tickets/{id}/comments", new { body = "Received, checking the RIB.", isInternal = false });

        // Monday 09:45: 90 + 105 = 195 of 240 minutes used (81%).
        api.Clock.Now = Tn(3, 23, 9, 45);
        await RunMonitorAsync(api);
        var atRisk = (await payroll.GetFromJsonAsync<CaseView>($"/api/tickets/{id}"))!;
        atRisk.Sla.State.Should().Be("AtRisk");
        (await NotificationsAsync(payroll, id)).Select(n => n.Type).Should().Contain(["SlaAtRisk", "Escalated"]);

        // Monday 10:40: past the 10:30 deadline.
        api.Clock.Now = Tn(3, 23, 10, 40);
        await RunMonitorAsync(api);
        var breached = (await payroll.GetFromJsonAsync<CaseView>($"/api/tickets/{id}"))!;
        breached.Sla.State.Should().Be("Breached");
        breached.Priority.Should().Be("Critical", "the breach rule raises High to Critical");
        // Sami has no manager: the "alert the manager" rule falls back to HR Admins.
        (await NotificationsAsync(admin, id)).Should().Contain(n => n.Type == "Escalated" && n.Message.Contains("Alert the manager", StringComparison.Ordinal));

        var escalations = breached.Timeline.Count(e => e.Type == "Escalated");
        escalations.Should().Be(3, "at-risk warning, manager alert and priority raise");

        api.Clock.Now = Tn(3, 23, 11, 0);
        await RunMonitorAsync(api);
        await RunMonitorAsync(api);
        (await payroll.GetFromJsonAsync<CaseView>($"/api/tickets/{id}"))!.Timeline.Count(e => e.Type == "Escalated")
            .Should().Be(escalations, "a rule fires only once per case");
    }

    [Fact]
    public async Task Unanswered_cases_are_chased_after_four_business_hours()
    {
        await using var api = new ClockedApiFactory(postgres.ConnectionString, Tn(3, 16, 9)); // Monday 09:00
        var employee = await SignedInAs(api, DemoUsers.AcmeEmployee);
        var created = await employee.SubmitWorkCertificateAsync("Nobody answers");
        var officer = await SignedInAs(api, DemoUsers.AcmeHrOfficer);

        api.Clock.Now = Tn(3, 16, 13, 30); // 3h30 of business time (lunch excluded)
        await RunMonitorAsync(api);
        (await officer.GetFromJsonAsync<CaseView>($"/api/tickets/{created.Id}"))!.Timeline.Should().NotContain(e => e.Type == "Escalated");

        api.Clock.Now = Tn(3, 16, 14, 0); // 4h
        await RunMonitorAsync(api);
        var timeline = (await officer.GetFromJsonAsync<CaseView>($"/api/tickets/{created.Id}"))!.Timeline;
        timeline.Should().ContainSingle(e => e.Type == "Escalated").Which.Data!["rule"]!.GetValue<string>()
            .Should().Be("Chase unanswered cases after 4 business hours");
    }

    [Fact]
    public async Task Hr_admins_manage_escalation_rules()
    {
        var admin = postgres.Api.CreateApiClient();
        admin.Authorize(await admin.LoginAsync(DemoUsers.AcmeHrAdmin));
        var officer = postgres.Api.CreateApiClient();
        officer.Authorize(await officer.LoginAsync(DemoUsers.AcmeHrOfficer));

        var rules = (await admin.GetFromJsonAsync<Rule[]>("/api/escalation-rules"))!;
        rules.Select(r => r.Name).Should().Contain("Alert the manager on breach");

        var noTeam = await admin.PostAsJsonAsync("/api/escalation-rules", new
        {
            name = "Hand over", trigger = "Breached", action = "ReassignToTeam", targetTeamId = (Guid?)null, isActive = true,
        });
        noTeam.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await noTeam.ProblemCodeAsync()).Should().Be("escalation.missing_team");

        (await officer.GetAsync("/api/escalation-rules")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
