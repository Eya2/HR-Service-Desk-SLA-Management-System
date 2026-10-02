using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using HrServiceDesk.IntegrationTests.Infrastructure;

namespace HrServiceDesk.IntegrationTests.Approvals;

[Collection(PostgresCollection.Name)]
public sealed class ApprovalWorkflowTests(PostgresFixture postgres)
{
    private readonly ApiFactory _api = postgres.Api;

    private sealed record Pending(Guid ApprovalId, Guid TicketId, string Reference, string RequesterName, string StepName, int StepOrder, int StepCount);

    private sealed record Workflow(Guid RequestTypeId, string RequestTypeName, bool IsConfigured, bool IsActive, int Version, Step[] Steps);

    private sealed record Step(int Order, string Name, string ApproverRole);

    private sealed record TeamStats(int TeamSize, int OpenCases, int SubmittedLast30Days, int PendingMyApproval);

    private async Task<HttpClient> SignedInAs(string email, string password = DemoUsers.Password)
    {
        var client = _api.CreateApiClient();
        return client.Authorize(await client.LoginAsync(email, password));
    }

    private static async Task<TicketDetails> GetAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<TicketDetails>($"/api/tickets/{id}"))!;

    private static Task<HttpResponseMessage> DecideAsync(HttpClient client, Guid ticketId, Guid approvalId, bool approve, string? comment = null) =>
        client.PostAsJsonAsync($"/api/tickets/{ticketId}/approvals/{approvalId}/decision", new { approve, comment });

    private static async Task<Pending[]> QueueAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<Pending[]>("/api/approvals/pending"))!;

    private static async Task<Created> SubmitSalaryAdvanceAsync(HttpClient client)
    {
        var typeId = await client.RequestTypeIdAsync("Salary advance");
        var response = await client.SubmitAsync(typeId, "Advance for rent", new JsonObject
        {
            ["amount"] = 800, ["repaymentMonths"] = 4, ["reason"] = "Deposit for a new flat.",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<Created>())!;
    }

    [Fact]
    public async Task Manager_approves_a_payslip_correction_which_then_opens()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var manager = await SignedInAs(DemoUsers.AcmeManager);
        var created = await employee.SubmitPayslipCorrectionAsync();

        var pending = (await GetAsync(employee, created.Id)).Approvals.Should().ContainSingle().Subject;
        pending.Should().BeEquivalentTo(new { StepName = "Manager approval", ApproverRole = "Manager", ApproverName = "Youssef Haddad", Decision = "Pending" });

        var item = (await QueueAsync(manager)).Should().ContainSingle(p => p.TicketId == created.Id).Subject;
        item.RequesterName.Should().Be("Amira Ben Salah");
        (await GetAsync(manager, created.Id)).Permissions.DecidableApprovalId.Should().Be(item.ApprovalId);
        (await GetAsync(employee, created.Id)).Permissions.DecidableApprovalId.Should().BeNull();

        (await DecideAsync(manager, created.Id, item.ApprovalId, approve: true, "OK for me")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var ticket = await GetAsync(employee, created.Id);
        ticket.Status.Should().Be("Open");
        ticket.Approvals.Single().Should().BeEquivalentTo(new { Decision = "Approved", DecidedByName = "Youssef Haddad", Comment = "OK for me" });
        ticket.Timeline.Select(e => e.Type).Should().Equal("Created", "StatusChanged", "ApprovalRequested", "ApprovalDecided", "StatusChanged", "Assigned");
        (await QueueAsync(manager)).Should().NotContain(p => p.TicketId == created.Id);
    }

    [Fact]
    public async Task Multi_step_workflow_moves_to_the_next_approver_role()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var manager = await SignedInAs(DemoUsers.AcmeManager);
        var payroll = await SignedInAs("sami.gharbi@acme.example");
        var officer = await SignedInAs(DemoUsers.AcmeHrOfficer);
        var created = await SubmitSalaryAdvanceAsync(employee);

        (await QueueAsync(payroll)).Should().NotContain(p => p.TicketId == created.Id, "the manager step comes first");
        var first = (await QueueAsync(manager)).Single(p => p.TicketId == created.Id);
        first.Should().BeEquivalentTo(new { StepOrder = 1, StepCount = 2 });
        await DecideAsync(manager, created.Id, first.ApprovalId, approve: true);

        (await GetAsync(employee, created.Id)).Status.Should().Be("PendingApproval");
        (await QueueAsync(officer)).Should().NotContain(p => p.TicketId == created.Id);
        var second = (await QueueAsync(payroll)).Single(p => p.TicketId == created.Id);
        second.StepName.Should().Be("Payroll validation");

        // A stale decision on the first step is refused.
        (await DecideAsync(manager, created.Id, first.ApprovalId, approve: true)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        // Someone without the step's role cannot decide it.
        (await DecideAsync(officer, created.Id, second.ApprovalId, approve: true)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await DecideAsync(payroll, created.Id, second.ApprovalId, approve: true);
        (await GetAsync(employee, created.Id)).Status.Should().Be("Open");
    }

    [Fact]
    public async Task Rejection_needs_a_reason_ends_the_flow_and_skips_later_steps()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var manager = await SignedInAs(DemoUsers.AcmeManager);
        var created = await SubmitSalaryAdvanceAsync(employee);
        var step = (await QueueAsync(manager)).Single(p => p.TicketId == created.Id);

        (await DecideAsync(manager, created.Id, step.ApprovalId, approve: false)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DecideAsync(manager, created.Id, step.ApprovalId, approve: false, "Advance already granted this year."))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var ticket = await GetAsync(employee, created.Id);
        ticket.Status.Should().Be("Rejected");
        ticket.Approvals.Select(a => a.Decision).Should().Equal("Rejected", "Skipped");
        ticket.Comments.Should().ContainSingle().Which.Body.Should().Be("Advance already granted this year.");
    }

    [Fact]
    public async Task Without_a_manager_the_manager_step_goes_to_hr_admins_and_nobody_approves_their_own_request()
    {
        var email = await TestUsers.CreateAsync(_api, "nomanager");
        var employee = await SignedInAs(email, TestUsers.Password);
        var admin = await SignedInAs(DemoUsers.AcmeHrAdmin);
        var created = await employee.SubmitPayslipCorrectionAsync();

        var approval = (await GetAsync(employee, created.Id)).Approvals.Single();
        approval.ApproverRole.Should().Be("HrAdmin");
        approval.StepName.Should().Contain("no manager");
        (await QueueAsync(admin)).Should().Contain(p => p.TicketId == created.Id);

        // The HR Admin's own payslip correction (they have no manager either) is not theirs to approve.
        var own = await admin.SubmitPayslipCorrectionAsync("My own payslip");
        (await QueueAsync(admin)).Should().NotContain(p => p.TicketId == own.Id);
        var ownStep = (await GetAsync(admin, own.Id)).Approvals.Single();
        (await DecideAsync(admin, own.Id, ownStep.Id, approve: true)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Cancelling_a_pending_request_removes_it_from_the_queue()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var manager = await SignedInAs(DemoUsers.AcmeManager);
        var created = await employee.SubmitPayslipCorrectionAsync();

        (await employee.ChangeStatusAsync(created.Id, "Cancelled")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await QueueAsync(manager)).Should().NotContain(p => p.TicketId == created.Id);
        (await GetAsync(employee, created.Id)).Approvals.Single().Decision.Should().Be("Skipped");
    }

    [Fact]
    public async Task Admin_edits_workflows_without_affecting_cases_in_flight()
    {
        var admin = await SignedInAs(DemoUsers.AcmeHrAdmin);
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var workflows = (await admin.GetFromJsonAsync<Workflow[]>("/api/workflows"))!;
        var training = workflows.Single(w => w.RequestTypeName == "Training request");
        training.Steps.Select(s => s.ApproverRole).Should().Equal("Manager", "HrOfficer");
        workflows.Single(w => w.RequestTypeName == "Work certificate").IsConfigured.Should().BeFalse();

        var typeId = training.RequestTypeId;
        var inFlight = await employee.SubmitAsync(typeId, "Course", new JsonObject
        {
            ["courseTitle"] = "Advanced Excel", ["provider"] = "Acme Academy", ["startDate"] = "2026-11-02", ["cost"] = 300,
            ["justification"] = "Payroll reporting.",
        });
        var inFlightId = (await inFlight.Content.ReadFromJsonAsync<Created>())!.Id;

        var saved = await admin.PutAsJsonAsync($"/api/workflows/{typeId}", new
        {
            isActive = true,
            steps = new[] { new { name = "Manager approval", approverRole = "Manager" }, new { name = "Budget", approverRole = "HrAdmin" }, new { name = "HR validation", approverRole = "HrOfficer" } },
        });
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await saved.Content.ReadFromJsonAsync<Workflow>())!.Should().BeEquivalentTo(new { Version = training.Version + 1 });

        (await GetAsync(employee, inFlightId)).Approvals.Should().HaveCount(2, "the case keeps the chain it was submitted with");
    }

    [Fact]
    public async Task Workflow_definitions_are_validated()
    {
        var admin = await SignedInAs(DemoUsers.AcmeHrAdmin);
        var workflows = (await admin.GetFromJsonAsync<Workflow[]>("/api/workflows"))!;
        var leave = workflows.Single(w => w.RequestTypeName == "Leave request").RequestTypeId;
        var harassment = workflows.Single(w => w.RequestTypeName == "Harassment report").RequestTypeId;

        var badRole = await admin.PutAsJsonAsync($"/api/workflows/{leave}", new { isActive = true, steps = new[] { new { name = "Peer", approverRole = "Employee" } } });
        badRole.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var empty = await admin.PutAsJsonAsync($"/api/workflows/{leave}", new { isActive = true, steps = Array.Empty<object>() });
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var confidential = await admin.PutAsJsonAsync($"/api/workflows/{harassment}", new { isActive = true, steps = new[] { new { name = "Manager", approverRole = "Manager" } } });
        confidential.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await confidential.ProblemCodeAsync()).Should().Be("workflow.confidential_approver");

        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        (await employee.GetAsync("/api/workflows")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Managers_see_their_team_requests_and_stats_but_not_confidential_cases()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var manager = await SignedInAs(DemoUsers.AcmeManager);
        var visible = await employee.SubmitWorkCertificateAsync("Team visible");
        var harassmentType = await employee.RequestTypeIdAsync("Harassment report");
        var report = await employee.SubmitAsync(harassmentType, "Report", new JsonObject
        {
            ["incidentDate"] = "2026-02-27", ["whatHappened"] = "Something that must stay confidential.",
        });
        var confidentialId = (await report.Content.ReadFromJsonAsync<Created>())!.Id;

        var team = (await manager.GetFromJsonAsync<TicketPage>("/api/team/tickets?pageSize=100"))!;
        team.Items.Should().Contain(t => t.Id == visible.Id).And.NotContain(t => t.Id == confidentialId);
        (await manager.GetAsync($"/api/tickets/{confidentialId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var stats = (await manager.GetFromJsonAsync<TeamStats>("/api/team/stats"))!;
        stats.TeamSize.Should().BeGreaterThanOrEqualTo(1);
        stats.SubmittedLast30Days.Should().BeGreaterThanOrEqualTo(1);

        (await employee.GetAsync("/api/team/tickets")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
