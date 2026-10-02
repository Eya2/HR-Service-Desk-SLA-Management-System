using System.Net;
using System.Net.Http.Json;
using HrServiceDesk.Application.Escalations;
using HrServiceDesk.IntegrationTests.Infrastructure;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace HrServiceDesk.IntegrationTests.Demo;

/// <summary>The demo history, generated into a database of its own so the other tests keep their known data.</summary>
[Collection(PostgresCollection.Name)]
public sealed class DemoHistoryTests(PostgresFixture postgres)
{
    private sealed record Kpis(int Created, int Resolved, int Backlog, double? SlaCompliancePercent, double? AverageSatisfaction, int Ratings);

    private sealed record Board(Kpis Kpis);

    private sealed record Summary(Guid Id, string Reference, string RequestTypeName, string Status, string SlaState);

    private sealed record Page(Summary[] Items, int TotalCount);

    [Fact]
    public async Task The_demo_history_fills_dashboards_queues_and_slas_and_respects_confidentiality()
    {
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = $"hrdesk_demo_{Guid.NewGuid():N}" }.ConnectionString;
        await using var api = new ApiFactory(connection, new Dictionary<string, string> { ["Seed:History"] = "true" });

        async Task<HttpClient> SignedInAs(string email)
        {
            var client = api.CreateApiClient();
            return client.Authorize(await client.LoginAsync(email));
        }

        var admin = await SignedInAs(DemoUsers.AcmeHrAdmin);
        var board = await admin.GetFromJsonAsync<Board>("/api/dashboard");
        board!.Kpis.Created.Should().BeGreaterThan(20);
        board.Kpis.Resolved.Should().BeGreaterThan(10);
        board.Kpis.Ratings.Should().BeGreaterThan(3);
        board.Kpis.SlaCompliancePercent.Should().BeInRange(40, 99.9, "most cases are on time, a few are late");

        // The live monitor finds the open cases that are now at risk or late, and escalates them.
        using (var scope = api.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunSlaMonitorCommand());
        var open = (await admin.GetFromJsonAsync<Page>("/api/tickets?scope=All&activeOnly=true&pageSize=100"))!.Items;
        open.Should().Contain(t => t.SlaState == "Breached");
        open.Should().Contain(t => t.SlaState == "AtRisk");

        // Confidential report: the restricted group sees it, other HR officers do not.
        open.Should().Contain(t => t.RequestTypeName == "Harassment report" && t.Status == "InProgress");
        var officer = await SignedInAs(DemoUsers.AcmeHrOfficer);
        (await officer.GetFromJsonAsync<Page>("/api/tickets?scope=All&activeOnly=false&pageSize=100"))!.Items
            .Should().NotContain(t => t.RequestTypeName == "Harassment report");

        // The extra employees can sign in and see their own history.
        var employee = await SignedInAs("mehdi.trabelsi@acme.example");
        (await employee.GetAsync("/api/tickets/mine")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Globex has its own history.
        var globex = await SignedInAs(DemoUsers.GlobexHrAdmin);
        (await globex.GetFromJsonAsync<Board>("/api/dashboard"))!.Kpis.Created.Should().BeGreaterThan(10);
    }
}
