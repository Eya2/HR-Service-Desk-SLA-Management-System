using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using HrServiceDesk.IntegrationTests.Infrastructure;

namespace HrServiceDesk.IntegrationTests.Dashboard;

/// <summary>
/// Globex in April 2024 (a period no other test uses), France calendar: Mon–Fri 09:00–12:30 / 13:30–18:00, UTC+2.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DashboardTests(PostgresFixture postgres)
{
    private const string Sophie = "sophie.laurent@globex.example";
    private const string Thomas = "thomas.petit@globex.example";

    private sealed record Kpis(int Created, int Resolved, int Backlog, double? SlaCompliancePercent, double? AverageFirstResponseHours,
        double? AverageResolutionHours, double? ReopenRatePercent);

    private sealed record Compliance(string Label, int Resolved, int Met, double? CompliancePercent);

    private sealed record Point(DateOnly Date, int Created, int Resolved);

    private sealed record Named(string Key, int Count);

    private sealed record TeamOption(Guid Id, string Name);

    private sealed record Board(string Granularity, Kpis Kpis, Compliance[] ComplianceByRequestType, Compliance[] ComplianceByTeam,
        Named[] BacklogByPriority, Named[] TopCategories, Point[] Volume, TeamOption[] Teams);

    private static DateTimeOffset Fr(int day, int hour, int minute = 0) => new(2024, 4, day, hour, minute, 0, TimeSpan.FromHours(2));

    private static readonly string Period =
        $"from={Uri.EscapeDataString(Fr(1, 0).ToString("o"))}&to={Uri.EscapeDataString(Fr(8, 0).ToString("o"))}";

    private static async Task<HttpClient> SignedInAs(ApiFactory api, string email)
    {
        var client = api.CreateApiClient();
        return client.Authorize(await client.LoginAsync(email));
    }

    private static async Task<Guid> SubmitBankDetailsAsync(HttpClient employee)
    {
        var typeId = await employee.RequestTypeIdAsync("Change of bank details");
        var response = await employee.SubmitAsync(typeId, "New bank", new JsonObject { ["bankName"] = "BNP", ["iban"] = "FR7630006000011234567890189" },
            new TestFile("bankCertificate", "rib.pdf", TicketApi.Pdf));
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }

    private static async Task ResolveAsync(HttpClient agent, Guid id)
    {
        (await agent.ChangeStatusAsync(id, "Open")).EnsureSuccessStatusCode();
        (await agent.ChangeStatusAsync(id, "Resolved", "Done.")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Dashboard_reports_volumes_sla_compliance_times_and_reopen_rate_for_the_period()
    {
        await using var api = new ClockedApiFactory(postgres.ConnectionString, Fr(1, 9));
        var employee = await SignedInAs(api, DemoUsers.GlobexEmployee);
        var admin = await SignedInAs(api, DemoUsers.GlobexHrAdmin);
        var officer = await SignedInAs(api, Sophie);
        var payroll = await SignedInAs(api, Thomas);

        // Monday 1 April: three work certificates.
        var cert1 = (await employee.SubmitWorkCertificateAsync("Cert 1")).Id;
        var cert2 = (await employee.SubmitWorkCertificateAsync("Cert 2")).Id;
        await employee.SubmitWorkCertificateAsync("Cert 3");
        api.Clock.Now = Fr(1, 11);
        await ResolveAsync(admin, cert1); // 2 business hours
        await ResolveAsync(admin, cert2);
        api.Clock.Now = Fr(1, 15);
        await employee.ChangeStatusAsync(cert2, "Reopened", "Wrong language");
        await admin.ChangeStatusAsync(cert2, "Resolved", "Fixed.");

        // Tuesday 2 April 09:00: two bank detail changes (Payroll policy, High: 240 business minutes).
        api.Clock.Now = Fr(2, 9);
        var bank1 = await SubmitBankDetailsAsync(employee);
        await SubmitBankDetailsAsync(employee);
        api.Clock.Now = Fr(4, 10);
        await ResolveAsync(payroll, bank1); // 8 + 8 + 1 = 17 business hours: breached

        var board = (await admin.GetFromJsonAsync<Board>($"/api/dashboard?{Period}"))!;
        board.Granularity.Should().Be("Day");
        board.Kpis.Should().BeEquivalentTo(new
        {
            Created = 5,
            Resolved = 3,
            SlaCompliancePercent = 66.7,
            ReopenRatePercent = 33.3,
        });
        board.ComplianceByRequestType.Should().ContainEquivalentOf(new { Label = "Change of bank details", Resolved = 1, Met = 0, CompliancePercent = 0.0 });
        board.ComplianceByRequestType.Should().ContainEquivalentOf(new { Label = "Work certificate", Resolved = 2, Met = 2, CompliancePercent = 100.0 });
        board.ComplianceByTeam.Select(c => c.Label).Should().Contain(["Payroll", "HR Service Center"]);
        board.TopCategories.Should().ContainEquivalentOf(new { Key = "Certificates", Count = 3 });
        board.Volume.Should().HaveCount(7);
        board.Volume[0].Should().BeEquivalentTo(new { Date = new DateOnly(2024, 4, 1), Created = 3, Resolved = 2 });
        board.Kpis.Backlog.Should().BeGreaterThanOrEqualTo(2);

        var payrollTeam = board.Teams.Single(t => t.Name == "Payroll").Id;
        var payrollBoard = (await admin.GetFromJsonAsync<Board>($"/api/dashboard?{Period}&teamId={payrollTeam}"))!;
        payrollBoard.Kpis.Created.Should().Be(2);
        payrollBoard.Kpis.AverageResolutionHours.Should().Be(17.0);

        // Confidential cases only count for the restricted group.
        var harassment = await employee.RequestTypeIdAsync("Harassment report");
        await employee.SubmitAsync(harassment, "Report", new JsonObject { ["incidentDate"] = "2024-03-29", ["whatHappened"] = "Something confidential happened." });
        (await admin.GetFromJsonAsync<Board>($"/api/dashboard?{Period}"))!.Kpis.Created.Should().Be(6);
        (await officer.GetFromJsonAsync<Board>($"/api/dashboard?{Period}"))!.Kpis.Created.Should().Be(5);

        var csv = await admin.GetAsync($"/api/dashboard/export?{Period}");
        csv.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        csv.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        var lines = (await csv.Content.ReadAsStringAsync()).TrimStart('﻿').Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().StartWith("Reference,Request type,Category");
        lines.Should().HaveCount(7, "a header and six cases");
        lines.Should().Contain(l => l.Contains("Change of bank details", StringComparison.Ordinal) && l.Contains(",17.0,", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Dashboards_are_for_hr_staff_and_auditors_and_validate_the_period()
    {
        var employee = postgres.Api.CreateApiClient();
        employee.Authorize(await employee.LoginAsync(DemoUsers.GlobexEmployee));
        (await employee.GetAsync("/api/dashboard")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var auditor = postgres.Api.CreateApiClient();
        auditor.Authorize(await auditor.LoginAsync("antoine.dubois@globex.example"));
        (await auditor.GetAsync("/api/dashboard")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await auditor.GetAsync("/api/dashboard?from=2026-01-01T00:00:00Z&to=2024-01-01T00:00:00Z")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await auditor.GetAsync("/api/dashboard/export?from=2020-01-01T00:00:00Z&to=2024-01-01T00:00:00Z")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
