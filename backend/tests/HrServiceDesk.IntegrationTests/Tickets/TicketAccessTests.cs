using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using HrServiceDesk.IntegrationTests.Infrastructure;

namespace HrServiceDesk.IntegrationTests.Tickets;

[Collection(PostgresCollection.Name)]
public sealed class TicketAccessTests(PostgresFixture postgres)
{
    private readonly ApiFactory _api = postgres.Api;

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = _api.CreateApiClient();
        return client.Authorize(await client.LoginAsync(email));
    }

    [Fact]
    public async Task My_requests_only_lists_the_callers_cases()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var mine = await employee.SubmitPayslipCorrectionAsync("Only mine");

        var page = (await employee.GetFromJsonAsync<TicketPage>("/api/tickets/mine?search=only%20mine"))!;
        page.Items.Should().Contain(t => t.Id == mine.Id);

        var manager = await SignedInAs(DemoUsers.AcmeManager);
        (await manager.GetFromJsonAsync<TicketPage>("/api/tickets/mine"))!.Items.Should().NotContain(t => t.Id == mine.Id);
    }

    [Fact]
    public async Task Cases_are_hidden_from_other_employees_and_other_organisations()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var created = await employee.SubmitPayslipCorrectionAsync();
        var colleague = _api.CreateApiClient();
        colleague.Authorize(await colleague.LoginAsync(await TestUsers.CreateAsync(_api, "colleague"), TestUsers.Password));

        (await colleague.GetAsync($"/api/tickets/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        // Amira's manager sees her (non-confidential) requests.
        (await (await SignedInAs(DemoUsers.AcmeManager)).GetAsync($"/api/tickets/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await (await SignedInAs(DemoUsers.GlobexHrAdmin)).GetAsync($"/api/tickets/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await (await SignedInAs(DemoUsers.AcmeHrOfficer)).GetAsync($"/api/tickets/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Only_hr_staff_and_auditors_can_list_all_cases()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var created = await employee.SubmitPayslipCorrectionAsync();

        (await employee.GetAsync("/api/tickets")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        foreach (var email in new[] { DemoUsers.AcmeHrOfficer, DemoUsers.AcmeAuditor })
        {
            var page = (await (await SignedInAs(email)).GetFromJsonAsync<TicketPage>($"/api/tickets?search={created.Reference}"))!;
            page.Items.Should().ContainSingle(t => t.Id == created.Id).Which.RequesterName.Should().Be("Amira Ben Salah");
        }

        var globex = (await (await SignedInAs(DemoUsers.GlobexHrAdmin)).GetFromJsonAsync<TicketPage>($"/api/tickets?search={created.Reference}"))!;
        globex.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Confidential_cases_are_visible_only_to_the_requester_and_hr_admins()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var typeId = await employee.RequestTypeIdAsync("Harassment report");
        var response = await employee.SubmitAsync(typeId, "Report", new JsonObject
        {
            ["incidentDate"] = "2026-02-27",
            ["whatHappened"] = "Repeated inappropriate remarks during team meetings.",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<Created>())!;

        (await employee.GetFromJsonAsync<TicketDetails>($"/api/tickets/{created.Id}"))!.IsConfidential.Should().BeTrue();
        (await (await SignedInAs(DemoUsers.AcmeHrAdmin)).GetAsync($"/api/tickets/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var email in new[] { DemoUsers.AcmeHrOfficer, DemoUsers.AcmeAuditor, DemoUsers.AcmeManager })
        {
            var client = await SignedInAs(email);
            (await client.GetAsync($"/api/tickets/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        var officerList = (await (await SignedInAs(DemoUsers.AcmeHrOfficer)).GetFromJsonAsync<TicketPage>($"/api/tickets?search={created.Reference}"))!;
        officerList.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Internal_notes_are_for_hr_staff_only()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var created = await employee.SubmitPayslipCorrectionAsync();
        var officer = await SignedInAs(DemoUsers.AcmeHrOfficer);

        (await employee.PostAsJsonAsync($"/api/tickets/{created.Id}/comments", new { body = "Any update?", isInternal = false }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var forbidden = await employee.PostAsJsonAsync($"/api/tickets/{created.Id}/comments", new { body = "Sneaky", isInternal = true });
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await forbidden.ProblemCodeAsync()).Should().Be("comment.internal_forbidden");

        var note = await officer.PostAsJsonAsync($"/api/tickets/{created.Id}/comments", new { body = "Check timesheet first", isInternal = true });
        note.StatusCode.Should().Be(HttpStatusCode.OK);
        (await officer.PostAsJsonAsync($"/api/tickets/{created.Id}/comments", new { body = "We are on it.", isInternal = false }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var employeeView = (await employee.GetFromJsonAsync<TicketDetails>($"/api/tickets/{created.Id}"))!;
        employeeView.Comments.Select(c => c.Body).Should().Equal("Any update?", "We are on it.");

        var officerView = (await officer.GetFromJsonAsync<TicketDetails>($"/api/tickets/{created.Id}"))!;
        officerView.Comments.Should().Contain(c => c.IsInternal && c.Body == "Check timesheet first" && c.AuthorName == "Leila Mansour");
        officerView.Permissions.CanCommentInternally.Should().BeTrue();
    }

    [Fact]
    public async Task Auditors_read_but_cannot_change_cases()
    {
        var created = await (await SignedInAs(DemoUsers.AcmeEmployee)).SubmitPayslipCorrectionAsync();
        var auditor = await SignedInAs(DemoUsers.AcmeAuditor);

        var view = (await auditor.GetFromJsonAsync<TicketDetails>($"/api/tickets/{created.Id}"))!;
        view.Permissions.Should().BeEquivalentTo(new Permissions(false, false, false, false, false, []));

        var comment = await auditor.PostAsJsonAsync($"/api/tickets/{created.Id}/comments", new { body = "Audit", isInternal = false });
        comment.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await comment.ProblemCodeAsync()).Should().Be("ticket.read_only");
    }

    [Fact]
    public async Task Requester_rewords_a_new_case_but_only_staff_change_priority()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var created = await employee.SubmitWorkCertificateAsync();

        (await employee.PutAsJsonAsync($"/api/tickets/{created.Id}", new { title = "Reworded title", description = "More detail", priority = "Low" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var escalate = await employee.PutAsJsonAsync($"/api/tickets/{created.Id}", new { title = "Reworded title", description = "", priority = "Critical" });
        escalate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await escalate.ProblemCodeAsync()).Should().Be("ticket.priority_forbidden");

        var officer = await SignedInAs(DemoUsers.AcmeHrOfficer);
        (await officer.PutAsJsonAsync($"/api/tickets/{created.Id}", new { title = "Reworded title", description = "More detail", priority = "Critical" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var ticket = (await employee.GetFromJsonAsync<TicketDetails>($"/api/tickets/{created.Id}"))!;
        ticket.Title.Should().Be("Reworded title");
        ticket.Description.Should().Be("More detail");
        ticket.Priority.Should().Be("Critical");
    }

    [Fact]
    public async Task Documents_can_be_added_to_an_existing_case()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var created = await employee.SubmitPayslipCorrectionAsync();
        using var content = new MultipartFormDataContent { { new ByteArrayContent(TicketApi.Pdf), "files", "timesheet.pdf" } };

        var response = await employee.PostAsync($"/api/tickets/{created.Id}/attachments", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ticket = (await employee.GetFromJsonAsync<TicketDetails>($"/api/tickets/{created.Id}"))!;
        ticket.Attachments.Select(a => a.FileName).Should().BeEquivalentTo("payslip-march.pdf", "timesheet.pdf");
        ticket.Attachments.Single(a => a.FileName == "timesheet.pdf").FieldKey.Should().BeNull();
    }
}
