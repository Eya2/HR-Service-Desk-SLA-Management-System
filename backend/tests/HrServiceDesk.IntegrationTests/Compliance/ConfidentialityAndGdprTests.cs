using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using HrServiceDesk.IntegrationTests.Infrastructure;

namespace HrServiceDesk.IntegrationTests.Compliance;

[Collection(PostgresCollection.Name)]
public sealed class ConfidentialityAndGdprTests(PostgresFixture postgres)
{
    private readonly ApiFactory _api = postgres.Api;

    private sealed record Team(Guid Id, string Name, string Strategy, bool IsConfidentialGroup, TeamMember[] Members);

    private sealed record TeamMember(Guid Id, string FullName);

    private sealed record AuditEntry(Guid Id, string UserName, string Action, string EntityType, Guid? EntityId, string Summary);

    private sealed record AuditPage(AuditEntry[] Items, int TotalCount);

    private sealed record Retention(int RetentionMonths);

    private static async Task<HttpClient> SignedInAs(ApiFactory api, string email, string password = DemoUsers.Password)
    {
        var client = api.CreateApiClient();
        return client.Authorize(await client.LoginAsync(email, password));
    }

    private static async Task<Guid> SubmitHarassmentReportAsync(HttpClient employee)
    {
        var typeId = await employee.RequestTypeIdAsync("Harassment report");
        var response = await employee.SubmitAsync(typeId, "Report", new JsonObject
        {
            ["incidentDate"] = "2026-02-27", ["whatHappened"] = "Something that must stay confidential.",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Created>())!.Id;
    }

    private static async Task<AuditEntry[]> AuditAsync(HttpClient client, Guid entityId) =>
        (await client.GetFromJsonAsync<AuditPage>("/api/audit-logs?pageSize=200"))!.Items.Where(e => e.EntityId == entityId).ToArray();

    [Fact]
    public async Task Confidential_cases_follow_the_restricted_group_not_the_role()
    {
        var employee = await SignedInAs(_api, DemoUsers.AcmeEmployee);
        var admin = await SignedInAs(_api, DemoUsers.AcmeHrAdmin);
        var caseId = await SubmitHarassmentReportAsync(employee);

        // Another HR Admin outside the restricted group cannot see the case.
        var otherAdminEmail = await TestUsers.CreateAsync(_api, "otheradmin", roles: ["Employee", "HrAdmin"]);
        var otherAdmin = await SignedInAs(_api, otherAdminEmail, TestUsers.Password);
        (await otherAdmin.GetAsync($"/api/tickets/{caseId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // An HR officer added to the group can.
        var officerEmail = await TestUsers.CreateAsync(_api, "groupofficer", roles: ["Employee", "HrOfficer"]);
        var officer = await SignedInAs(_api, officerEmail, TestUsers.Password);
        var officerId = (await officer.GetFromJsonAsync<UserProfile>("/api/auth/me"))!.Id;
        var group = (await admin.GetFromJsonAsync<Team[]>("/api/teams"))!.Single(t => t.IsConfidentialGroup);
        group.Name.Should().Be("Confidential HR");
        (await admin.PutAsJsonAsync($"/api/teams/{group.Id}", new
        {
            name = group.Name, strategy = group.Strategy, isConfidentialGroup = true,
            memberIds = group.Members.Select(m => m.Id).Append(officerId).ToArray(),
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await officer.GetAsync($"/api/tickets/{caseId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Assignment stays within the group.
        var outsiderId = (await otherAdmin.GetFromJsonAsync<UserProfile>("/api/auth/me"))!.Id;
        var outside = await admin.PutAsJsonAsync($"/api/tickets/{caseId}/assignee", new { assigneeId = outsiderId });
        outside.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await outside.ProblemCodeAsync()).Should().Be("ticket.assignee_not_in_confidential_group");
        (await admin.PutAsJsonAsync($"/api/tickets/{caseId}/assignee", new { assigneeId = officerId })).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Views_and_downloads_of_sensitive_cases_by_hr_are_audited()
    {
        var employee = await SignedInAs(_api, DemoUsers.AcmeEmployee);
        var payroll = await SignedInAs(_api, "sami.gharbi@acme.example");
        var auditor = await SignedInAs(_api, DemoUsers.AcmeAuditor);
        var typeId = await employee.RequestTypeIdAsync("Change of bank details");
        var response = await employee.SubmitAsync(typeId, "New bank", new JsonObject { ["bankName"] = "BIAT", ["iban"] = "TN5910006035183598478831" },
            new TestFile("bankCertificate", "rib.pdf", TicketApi.Pdf));
        var id = (await response.Content.ReadFromJsonAsync<Created>())!.Id;

        await employee.GetAsync($"/api/tickets/{id}");
        (await AuditAsync(auditor, id)).Should().BeEmpty("the requester reading their own case is not audited");

        var details = (await payroll.GetFromJsonAsync<TicketDetails>($"/api/tickets/{id}"))!;
        await payroll.GetAsync($"/api/tickets/{id}/attachments/{details.Attachments[0].Id}");

        var entries = await AuditAsync(auditor, id);
        entries.Should().ContainSingle(e => e.Action == "SensitiveCaseViewed" && e.UserName == "Sami Gharbi");
        var download = (await auditor.GetFromJsonAsync<AuditPage>("/api/audit-logs?action=SensitiveAttachmentDownloaded&pageSize=200"))!.Items;
        download.Should().Contain(e => e.EntityId == details.Attachments[0].Id);
        entries.Should().OnlyContain(e => !e.Summary.Contains("TN59", StringComparison.Ordinal), "the log never holds the sensitive values");

        (await employee.GetAsync("/api/audit-logs")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task User_administration_is_audited()
    {
        var auditor = await SignedInAs(_api, DemoUsers.AcmeAuditor);
        var email = await TestUsers.CreateAsync(_api, "audited");
        var userId = (await (await SignedInAs(_api, email, TestUsers.Password)).GetFromJsonAsync<UserProfile>("/api/auth/me"))!.Id;

        var admin = await SignedInAs(_api, DemoUsers.AcmeHrAdmin);
        await admin.PostAsJsonAsync($"/api/users/{userId}/reset-password", new { newPassword = "Another-Passw0rd!" });

        (await AuditAsync(auditor, userId)).Select(e => e.Action).Should().Contain(["UserCreated", "PasswordReset"]);
    }

    [Fact]
    public async Task Closed_cases_past_the_retention_period_are_anonymized()
    {
        // Globex, on its own clock: a case closed in January 2025, retention set to 6 months, job run in August.
        await using var api = new ClockedApiFactory(postgres.ConnectionString, new DateTimeOffset(2025, 1, 13, 9, 0, 0, TimeSpan.FromHours(1)));
        var employee = await SignedInAs(api, DemoUsers.GlobexEmployee);
        var admin = await SignedInAs(api, DemoUsers.GlobexHrAdmin);

        (await admin.PutAsJsonAsync("/api/settings/retention", new { retentionMonths = 3 })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await admin.PutAsJsonAsync("/api/settings/retention", new { retentionMonths = 6 })).StatusCode.Should().Be(HttpStatusCode.OK);

        try
        {
            var typeId = await employee.RequestTypeIdAsync("Change of bank details");
            var response = await employee.SubmitAsync(typeId, "Compte chez Mme Durand", new JsonObject { ["bankName"] = "BNP", ["iban"] = "FR7630006000011234567890189" },
                new TestFile("bankCertificate", "rib.pdf", TicketApi.Pdf));
            var old = (await response.Content.ReadFromJsonAsync<Created>())!;
            await employee.PostAsJsonAsync($"/api/tickets/{old.Id}/comments", new { body = "My personal phone is 06 12 34 56 78", isInternal = false });
            await employee.ChangeStatusAsync(old.Id, "Cancelled", "Changed my mind");

            api.Clock.Now = new DateTimeOffset(2025, 7, 20, 9, 0, 0, TimeSpan.FromHours(2));
            var recent = await employee.SubmitWorkCertificateAsync("Still needed");
            await employee.ChangeStatusAsync(recent.Id, "Cancelled");

            api.Clock.Now = new DateTimeOffset(2025, 8, 1, 9, 0, 0, TimeSpan.FromHours(2));
            var run = await admin.PostAsync("/api/settings/retention/run", null);
            (await run.Content.ReadFromJsonAsync<JsonObject>())!["anonymized"]!.GetValue<int>().Should().BeGreaterThanOrEqualTo(1);

            var anonymized = (await admin.GetFromJsonAsync<TicketDetails>($"/api/tickets/{old.Id}"))!;
            anonymized.Title.Should().Be("Anonymized case");
            anonymized.Answers.Should().BeEmpty();
            anonymized.Attachments.Should().BeEmpty();
            anonymized.Comments.Should().OnlyContain(c => c.Body == "[removed]");
            anonymized.Timeline.Select(e => e.Data?.ToString() ?? string.Empty).Should().NotContain(d => d.Contains("Changed my mind", StringComparison.Ordinal));
            (await admin.GetFromJsonAsync<JsonObject>($"/api/tickets/{old.Id}"))!["requester"]!["fullName"]!.GetValue<string>().Should().Be("Former employee");

            (await employee.GetAsync($"/api/tickets/{old.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "the case is no longer linked to the employee");
            (await employee.GetFromJsonAsync<TicketDetails>($"/api/tickets/{recent.Id}"))!.Title.Should().Be("Still needed");

            var audit = (await admin.GetFromJsonAsync<AuditPage>("/api/audit-logs?action=CaseAnonymized&pageSize=200"))!.Items;
            audit.Should().Contain(e => e.EntityId == old.Id && e.UserName == "Claire Moreau");
        }
        finally
        {
            await admin.PutAsJsonAsync("/api/settings/retention", new { retentionMonths = 24 });
        }
    }
}
