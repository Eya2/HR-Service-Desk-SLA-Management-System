using System.Net;
using System.Net.Http.Json;
using HrServiceDesk.IntegrationTests.Infrastructure;

namespace HrServiceDesk.IntegrationTests.Tickets;

[Collection(PostgresCollection.Name)]
public sealed class TicketLifecycleTests(PostgresFixture postgres)
{
    private readonly ApiFactory _api = postgres.Api;

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = _api.CreateApiClient();
        return client.Authorize(await client.LoginAsync(email));
    }

    private static async Task<TicketDetails> GetAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<TicketDetails>($"/api/tickets/{id}"))!;

    [Fact]
    public async Task A_case_goes_from_submission_to_closure_with_a_full_audit_trail()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var officer = await SignedInAs(DemoUsers.AcmeHrOfficer);
        var created = await employee.SubmitWorkCertificateAsync();

        (await GetAsync(officer, created.Id)).Permissions.AvailableTransitions.Should().Equal("Open", "Rejected", "Cancelled");

        (await officer.ChangeStatusAsync(created.Id, "Open")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await officer.ChangeStatusAsync(created.Id, "InProgress")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await officer.ChangeStatusAsync(created.Id, "WaitingOnEmployee", "Please send your March timesheet."))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The employee's answer puts the case back in progress automatically.
        (await employee.PostAsJsonAsync($"/api/tickets/{created.Id}/comments", new { body = "Timesheet attached.", isInternal = false }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetAsync(officer, created.Id)).Status.Should().Be("InProgress");

        (await officer.ChangeStatusAsync(created.Id, "Resolved", "Corrected on the April payslip.")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync(employee, created.Id)).Permissions.AvailableTransitions.Should().Equal("Closed", "Reopened");
        (await employee.ChangeStatusAsync(created.Id, "Closed")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var ticket = await GetAsync(employee, created.Id);
        ticket.Status.Should().Be("Closed");
        ticket.Approvals.Should().BeEmpty("work certificates need no approval");
        ticket.Permissions.AvailableTransitions.Should().BeEmpty();
        ticket.Permissions.CanComment.Should().BeFalse();
        ticket.Comments.Select(c => c.Body).Should().Equal(
            "Please send your March timesheet.", "Timesheet attached.", "Corrected on the April payslip.");

        var statuses = ticket.Timeline.Where(e => e.Type == "StatusChanged").Select(e => e.Data!["to"]!.GetValue<string>());
        statuses.Should().Equal("Open", "InProgress", "WaitingOnEmployee", "InProgress", "Resolved", "Closed");
        ticket.Timeline.First().Type.Should().Be("Created");
        ticket.Timeline.Where(e => e.Type == "StatusChanged").Select(e => e.ActorName).Should().Equal(
            "Leila Mansour", "Leila Mansour", "Leila Mansour", "Amira Ben Salah", "Leila Mansour", "Amira Ben Salah");
    }

    [Fact]
    public async Task Invalid_transitions_are_422_and_unauthorised_ones_403()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var created = await employee.SubmitWorkCertificateAsync();

        var invalid = await employee.ChangeStatusAsync(created.Id, "Closed");
        invalid.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await invalid.ProblemCodeAsync()).Should().Be("ticket.invalid_transition");
        (await invalid.Content.ReadAsStringAsync()).Should().Contain("cannot move from New to Closed");

        var forbidden = await employee.ChangeStatusAsync(created.Id, "Open");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await forbidden.ProblemCodeAsync()).Should().Be("ticket.transition_not_permitted");

        var auditor = await SignedInAs(DemoUsers.AcmeAuditor);
        (await auditor.ChangeStatusAsync(created.Id, "Open")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await employee.ChangeStatusAsync(created.Id, "Bogus")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetAsync(employee, created.Id)).Status.Should().Be("New");
    }

    [Fact]
    public async Task Rejecting_requires_a_reason_that_the_employee_sees()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var officer = await SignedInAs(DemoUsers.AcmeHrOfficer);
        var created = await employee.SubmitWorkCertificateAsync();

        (await officer.ChangeStatusAsync(created.Id, "Rejected")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await officer.ChangeStatusAsync(created.Id, "Rejected", "Overtime was paid in February.")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var ticket = await GetAsync(employee, created.Id);
        ticket.Status.Should().Be("Rejected");
        ticket.Comments.Should().ContainSingle().Which.Body.Should().Be("Overtime was paid in February.");
        ticket.Timeline.Last().Data!["reason"]!.GetValue<string>().Should().Be("Overtime was paid in February.");
    }

    [Fact]
    public async Task Employee_can_withdraw_and_reopen_but_not_after_closure()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var officer = await SignedInAs(DemoUsers.AcmeHrOfficer);

        var withdrawn = await employee.SubmitWorkCertificateAsync();
        (await employee.ChangeStatusAsync(withdrawn.Id, "Cancelled")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await officer.ChangeStatusAsync(withdrawn.Id, "Open")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var reopened = await employee.SubmitWorkCertificateAsync();
        await officer.ChangeStatusAsync(reopened.Id, "Open");
        await officer.ChangeStatusAsync(reopened.Id, "Resolved");
        (await officer.ChangeStatusAsync(reopened.Id, "Reopened")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await employee.ChangeStatusAsync(reopened.Id, "Reopened")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync(officer, reopened.Id)).Permissions.AvailableTransitions.Should().Equal("InProgress", "WaitingOnEmployee", "Resolved", "Cancelled");
    }

    [Fact]
    public async Task Internal_notes_never_appear_in_the_employees_timeline()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var officer = await SignedInAs(DemoUsers.AcmeHrOfficer);
        var created = await employee.SubmitWorkCertificateAsync();

        await officer.PostAsJsonAsync($"/api/tickets/{created.Id}/comments", new { body = "Check with payroll", isInternal = true });

        (await GetAsync(employee, created.Id)).Timeline.Should().NotContain(e => e.Type == "CommentAdded");
        (await GetAsync(officer, created.Id)).Timeline.Should().ContainSingle(e => e.Type == "CommentAdded");
    }
}
