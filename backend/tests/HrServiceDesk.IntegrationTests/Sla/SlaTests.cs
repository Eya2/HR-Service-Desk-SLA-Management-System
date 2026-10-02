using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using HrServiceDesk.IntegrationTests.Infrastructure;

namespace HrServiceDesk.IntegrationTests.Sla;

/// <summary>
/// SLA deadlines on the Acme Tunisie calendar (Mon–Fri 08–12 / 13–17, UTC+1; 20 March 2026 is Independence Day).
/// Each test runs on its own host with a hand-moved clock.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SlaTests(PostgresFixture postgres)
{
    private sealed record SlaInfo(
        string State, bool IsPaused, DateTimeOffset? FirstResponseDueAt, DateTimeOffset? ResolutionDueAt, DateTimeOffset? FirstRespondedAt,
        bool FirstResponseBreached, bool ResolutionBreached, int? FirstResponseTargetMinutes, int? ResolutionTargetMinutes);

    private sealed record CaseWithSla(Guid Id, string Status, SlaInfo Sla, TimelineEntry[] Timeline, Approval[] Approvals);

    private sealed record Calendar(string Name, string TimeZoneId, Holiday[] Holidays);

    private sealed record Holiday(DateOnly Date, string Name);

    private sealed record Policy(Guid Id, string Name, bool IsDefault, string[] RequestTypes);

    /// <summary>Tunis local time.</summary>
    private static DateTimeOffset Tn(int month, int day, int hour, int minute = 0) => new(2026, month, day, hour, minute, 0, TimeSpan.FromHours(1));

    private static async Task<HttpClient> SignedInAs(ApiFactory api, string email)
    {
        var client = api.CreateApiClient();
        return client.Authorize(await client.LoginAsync(email));
    }

    private static async Task<CaseWithSla> GetAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<CaseWithSla>($"/api/tickets/{id}"))!;

    private static async Task<Guid> SubmitBankDetailsAsync(HttpClient employee)
    {
        var typeId = await employee.RequestTypeIdAsync("Change of bank details");
        var response = await employee.SubmitAsync(typeId, "New bank", new JsonObject { ["bankName"] = "BIAT", ["iban"] = "TN5910006035183598478831" },
            new TestFile("bankCertificate", "rib.pdf", TicketApi.Pdf));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }

    [Fact]
    public async Task Deadlines_are_in_business_hours_and_skip_a_public_holiday_and_the_weekend()
    {
        await using var api = new ClockedApiFactory(postgres.ConnectionString, Tn(3, 19, 15, 30)); // Thursday 15:30
        var employee = await SignedInAs(api, DemoUsers.AcmeEmployee);

        var id = await SubmitBankDetailsAsync(employee);

        var sla = (await GetAsync(employee, id)).Sla;
        sla.State.Should().Be("OnTrack");
        // "Payroll" policy, High priority: first response 60, resolution 240 business minutes.
        sla.FirstResponseTargetMinutes.Should().Be(60);
        sla.FirstResponseDueAt.Should().Be(Tn(3, 19, 16, 30));
        // 90 min on Thursday; Friday 20 is a holiday; weekend; then 150 min on Monday 23 (08:00–10:30).
        sla.ResolutionDueAt.Should().Be(Tn(3, 23, 10, 30));
    }

    [Fact]
    public async Task Pending_approval_and_waiting_on_the_employee_pause_the_clock()
    {
        await using var api = new ClockedApiFactory(postgres.ConnectionString, Tn(3, 16, 9)); // Monday 09:00
        var employee = await SignedInAs(api, DemoUsers.AcmeEmployee);
        var manager = await SignedInAs(api, DemoUsers.AcmeManager);
        var payroll = await SignedInAs(api, "sami.gharbi@acme.example");
        var created = await employee.SubmitPayslipCorrectionAsync();

        var pending = (await GetAsync(employee, created.Id)).Sla;
        pending.IsPaused.Should().BeTrue();
        pending.ResolutionDueAt.Should().BeNull();

        api.Clock.Now = Tn(3, 16, 14); // the manager approves at 14:00
        var approvalId = (await GetAsync(manager, created.Id)).Approvals.Single().Id;
        await manager.PostAsJsonAsync($"/api/tickets/{created.Id}/approvals/{approvalId}/decision", new { approve = true });

        var open = (await GetAsync(employee, created.Id)).Sla;
        open.IsPaused.Should().BeFalse();
        open.ResolutionDueAt.Should().Be(Tn(3, 17, 9), "240 business minutes from Monday 14:00: 14:00–17:00 on Monday, 08:00–09:00 on Tuesday");

        api.Clock.Now = Tn(3, 16, 15);
        await payroll.ChangeStatusAsync(created.Id, "WaitingOnEmployee", "Please send the March timesheet.");
        var waiting = (await GetAsync(employee, created.Id)).Sla;
        waiting.IsPaused.Should().BeTrue();
        waiting.FirstRespondedAt.Should().Be(Tn(3, 16, 15), "the question to the employee is HR's first response");

        api.Clock.Now = Tn(3, 17, 9); // the employee answers next morning at 09:00
        await employee.PostAsJsonAsync($"/api/tickets/{created.Id}/comments", new { body = "Attached.", isInternal = false });

        var resumed = await GetAsync(employee, created.Id);
        resumed.Status.Should().Be("InProgress");
        // Paused Monday 15:00–17:00 and Tuesday 08:00–09:00: 3 business hours added to Tuesday 09:00.
        resumed.Sla.ResolutionDueAt.Should().Be(Tn(3, 17, 12));
    }

    [Fact]
    public async Task Overdue_cases_are_reported_breached()
    {
        await using var api = new ClockedApiFactory(postgres.ConnectionString, Tn(3, 19, 15, 30));
        var employee = await SignedInAs(api, DemoUsers.AcmeEmployee);
        var id = await SubmitBankDetailsAsync(employee);

        api.Clock.Now = Tn(3, 23, 10); // Monday 10:00: 210 of 240 minutes used
        await employee.PostAsJsonAsync($"/api/tickets/{id}/comments", new { body = "Any news?", isInternal = false });
        (await GetAsync(employee, id)).Sla.State.Should().Be("Breached", "nobody answered within the 60 minutes first-response target");

        var timeline = (await GetAsync(employee, id)).Timeline;
        timeline.Should().Contain(e => e.Type == "SlaStateChanged" && e.Data!["to"]!.GetValue<string>() == "Breached");
    }

    [Fact]
    public async Task Calendar_and_policies_are_maintained_by_hr_admins()
    {
        await using var api = new ClockedApiFactory(postgres.ConnectionString, Tn(3, 19, 15, 30));
        var admin = await SignedInAs(api, DemoUsers.AcmeHrAdmin);
        var officer = await SignedInAs(api, DemoUsers.AcmeHrOfficer);

        var calendar = (await officer.GetFromJsonAsync<Calendar>("/api/calendar"))!;
        calendar.TimeZoneId.Should().Be("Africa/Tunis");
        calendar.Holidays.Should().Contain(h => h.Date == new DateOnly(2026, 3, 20));

        async Task<DateTimeOffset> Deadline() => await officer.GetFromJsonAsync<DateTimeOffset>(
            $"/api/calendar/deadline?start={Uri.EscapeDataString(Tn(3, 19, 16, 30).ToString("o"))}&minutes=60");
        (await Deadline()).Should().Be(Tn(3, 23, 8, 30));

        (await officer.PostAsJsonAsync("/api/calendar/holidays", new { date = "2026-03-23", name = "Test" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsJsonAsync("/api/calendar/holidays", new { date = "2026-03-23", name = "Company day" })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Deadline()).Should().Be(Tn(3, 24, 8, 30));
        (await admin.DeleteAsync("/api/calendar/holidays/2026-03-23")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Deadline()).Should().Be(Tn(3, 23, 8, 30));

        var policies = (await officer.GetFromJsonAsync<Policy[]>("/api/sla-policies"))!;
        policies.Single(p => p.IsDefault).Name.Should().Be("Standard");
        policies.Single(p => p.Name == "Payroll").RequestTypes.Should().Contain("Payslip correction");

        var invalid = await admin.PostAsJsonAsync("/api/sla-policies", new
        {
            name = $"Partial {Guid.NewGuid():N}", atRiskThresholdPercent = 80, isDefault = false,
            targets = new[] { new { priority = "Low", firstResponseMinutes = 60, resolutionMinutes = 120 } },
            pauseStatuses = Array.Empty<string>(),
        });
        invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await invalid.ProblemCodeAsync()).Should().Be("sla.missing_targets");
    }
}
