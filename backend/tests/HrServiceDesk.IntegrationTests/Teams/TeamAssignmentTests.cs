using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using HrServiceDesk.IntegrationTests.Infrastructure;

namespace HrServiceDesk.IntegrationTests.Teams;

[Collection(PostgresCollection.Name)]
public sealed class TeamAssignmentTests(PostgresFixture postgres)
{
    private const string Sami = "sami.gharbi@acme.example";
    private readonly ApiFactory _api = postgres.Api;

    private sealed record Member(Guid Id, string FullName, int ActiveCases);

    private sealed record TeamDto(Guid Id, string Name, string Strategy, Member[] Members, string[] RequestTypes);

    private async Task<HttpClient> SignedInAs(string email, string password = DemoUsers.Password)
    {
        var client = _api.CreateApiClient();
        return client.Authorize(await client.LoginAsync(email, password));
    }

    private static async Task<TicketDetails> GetAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<TicketDetails>($"/api/tickets/{id}"))!;

    private static async Task<TeamDto[]> TeamsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<TeamDto[]>("/api/teams"))!;

    [Fact]
    public async Task Cases_are_routed_to_the_responsible_team_and_assigned_in_turn()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var officer = await SignedInAs(DemoUsers.AcmeHrOfficer);

        var assignees = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            var created = await employee.SubmitWorkCertificateAsync($"Round robin {i}");
            var ticket = await GetAsync(officer, created.Id);
            ticket.Team!.Name.Should().Be("HR Service Center");
            assignees.Add(ticket.Assignee!.FullName);
        }

        assignees.Distinct().Should().BeEquivalentTo("Leila Mansour", "Nadia Jaziri");
        assignees.Zip(assignees.Skip(1)).Should().OnlyContain(pair => pair.First != pair.Second, "round-robin alternates");
    }

    [Fact]
    public async Task Approved_cases_are_assigned_to_the_payroll_team_only_after_approval()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var manager = await SignedInAs(DemoUsers.AcmeManager);
        var created = await employee.SubmitPayslipCorrectionAsync();

        var pending = await GetAsync(manager, created.Id);
        pending.Team!.Name.Should().Be("Payroll");
        pending.Assignee.Should().BeNull("nobody works on a case before it is approved");

        var approvalId = pending.Approvals.Single().Id;
        await manager.PostAsJsonAsync($"/api/tickets/{created.Id}/approvals/{approvalId}/decision", new { approve = true });

        (await GetAsync(employee, created.Id)).Assignee!.FullName.Should().Be("Sami Gharbi");
    }

    [Fact]
    public async Task Manual_teams_leave_cases_in_the_queue()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var admin = await SignedInAs(DemoUsers.AcmeHrAdmin);
        var typeId = await employee.RequestTypeIdAsync("Harassment report");
        var response = await employee.SubmitAsync(typeId, "Report", new JsonObject
        {
            ["incidentDate"] = "2026-02-27", ["whatHappened"] = "Something that must stay confidential.",
        });
        var created = (await response.Content.ReadFromJsonAsync<Created>())!;

        var ticket = await GetAsync(admin, created.Id);
        ticket.Team!.Name.Should().Be("Confidential HR");
        ticket.Assignee.Should().BeNull();
        ticket.Permissions.Should().BeEquivalentTo(new { CanAssign = true, CanClaim = true });
    }

    [Fact]
    public async Task Two_agents_claiming_the_same_case_at_once_get_one_success_and_one_conflict()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var officer = await SignedInAs(DemoUsers.AcmeHrOfficer);
        var admin = await SignedInAs(DemoUsers.AcmeHrAdmin);

        for (var round = 0; round < 5; round++)
        {
            var created = await employee.SubmitWorkCertificateAsync($"Race {round}");
            (await officer.PutAsJsonAsync($"/api/tickets/{created.Id}/assignee", new { assigneeId = (Guid?)null }))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var results = await Task.WhenAll(
                officer.PostAsync($"/api/tickets/{created.Id}/claim", null),
                admin.PostAsync($"/api/tickets/{created.Id}/claim", null));

            results.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.NoContent, HttpStatusCode.Conflict]);
            var conflict = results.Single(r => r.StatusCode == HttpStatusCode.Conflict);
            // Sequential: the domain sees the holder; truly simultaneous: the xmin check fails. Both are 409.
            (await conflict.ProblemCodeAsync()).Should().BeOneOf("ticket.already_assigned", "concurrency.conflict");
        }
    }

    [Fact]
    public async Task Only_hr_staff_assign_and_only_to_hr_staff()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var officer = await SignedInAs(DemoUsers.AcmeHrOfficer);
        var created = await employee.SubmitWorkCertificateAsync();

        (await employee.PostAsync($"/api/tickets/{created.Id}/claim", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var employeeId = (await employee.GetFromJsonAsync<UserProfile>("/api/auth/me"))!.Id;
        var toEmployee = await officer.PutAsJsonAsync($"/api/tickets/{created.Id}/assignee", new { assigneeId = employeeId });
        toEmployee.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await toEmployee.ProblemCodeAsync()).Should().Be("ticket.invalid_assignee");

        var samiId = (await TeamsAsync(officer)).Single(t => t.Name == "Payroll").Members.Single().Id;
        (await officer.PutAsJsonAsync($"/api/tickets/{created.Id}/assignee", new { assigneeId = samiId })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync(officer, created.Id)).Assignee!.FullName.Should().Be("Sami Gharbi");
    }

    [Fact]
    public async Task Queue_views_filter_by_assignee_and_team()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var officer = await SignedInAs(DemoUsers.AcmeHrOfficer);
        var mine = await employee.SubmitWorkCertificateAsync("Queue view mine");
        // Round-robin may have given it to a colleague: release it, then take it.
        await officer.PutAsJsonAsync($"/api/tickets/{mine.Id}/assignee", new { assigneeId = (Guid?)null });
        (await officer.PostAsync($"/api/tickets/{mine.Id}/claim", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var free = await employee.SubmitWorkCertificateAsync("Queue view free");
        await officer.PutAsJsonAsync($"/api/tickets/{free.Id}/assignee", new { assigneeId = (Guid?)null });

        async Task<TicketSummary[]> List(string query) =>
            (await officer.GetFromJsonAsync<TicketPage>($"/api/tickets?pageSize=100&search=queue%20view&{query}"))!.Items;

        (await List("scope=Mine")).Select(t => t.Id).Should().Contain(mine.Id).And.NotContain(free.Id);
        (await List("scope=Unassigned")).Select(t => t.Id).Should().Contain(free.Id).And.NotContain(mine.Id);
        (await List("scope=MyTeams")).Select(t => t.Id).Should().Contain([mine.Id, free.Id]);
        (await List("scope=MyTeams")).Should().OnlyContain(t => t.TeamName == "HR Service Center");

        var sami = await SignedInAs(Sami);
        (await sami.GetFromJsonAsync<TicketPage>("/api/tickets?pageSize=100&search=queue%20view&scope=MyTeams"))!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Moving_a_case_to_another_team_reassigns_it_with_that_teams_strategy()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var officer = await SignedInAs(DemoUsers.AcmeHrOfficer);
        var created = await employee.SubmitWorkCertificateAsync("Belongs to payroll");
        var payroll = (await TeamsAsync(officer)).Single(t => t.Name == "Payroll");

        (await officer.PutAsJsonAsync($"/api/tickets/{created.Id}/team", new { teamId = payroll.Id })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var ticket = await GetAsync(officer, created.Id);
        ticket.Team!.Name.Should().Be("Payroll");
        ticket.Assignee!.FullName.Should().Be("Sami Gharbi");
        ticket.Timeline.Count(e => e.Type == "Assigned").Should().BeGreaterThanOrEqualTo(3, "initial assignment, move, and new assignment");
    }

    [Fact]
    public async Task Hr_admins_maintain_teams_and_routing()
    {
        var admin = await SignedInAs(DemoUsers.AcmeHrAdmin);
        var officer = await SignedInAs(DemoUsers.AcmeHrOfficer);
        var teams = await TeamsAsync(officer);
        teams.Select(t => t.Name).Should().Contain(["HR Service Center", "Payroll", "Confidential HR"]);
        teams.Single(t => t.Name == "Payroll").RequestTypes.Should().Contain("Payslip correction");

        var employeeId = (await (await SignedInAs(DemoUsers.AcmeEmployee)).GetFromJsonAsync<UserProfile>("/api/auth/me"))!.Id;
        var invalid = await admin.PostAsJsonAsync("/api/teams", new { name = $"Bad {Guid.NewGuid():N}", strategy = "RoundRobin", memberIds = new[] { employeeId } });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await invalid.ProblemCodeAsync()).Should().Be("team.invalid_members");

        var officerId = teams.Single(t => t.Name == "HR Service Center").Members.Single(m => m.FullName == "Leila Mansour").Id;
        var created = await admin.PostAsJsonAsync("/api/teams", new { name = $"Benefits {Guid.NewGuid():N}", strategy = "LeastLoaded", memberIds = new[] { officerId } });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var team = (await created.Content.ReadFromJsonAsync<TeamDto>())!;
        team.Members.Should().ContainSingle().Which.FullName.Should().Be("Leila Mansour");

        (await officer.PostAsJsonAsync("/api/teams", new { name = "Not allowed", strategy = "Manual", memberIds = Array.Empty<Guid>() }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
