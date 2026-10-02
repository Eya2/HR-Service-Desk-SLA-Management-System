using System.Net;
using System.Net.Http.Json;
using HrServiceDesk.IntegrationTests.Infrastructure;

namespace HrServiceDesk.IntegrationTests.Integration;

[Collection(PostgresCollection.Name)]
public sealed class IntegrationApiTests(PostgresFixture postgres)
{
    private sealed record KeyInfo(Guid Id, string Name, string Prefix, string[] Scopes, DateTimeOffset? LastUsedAt, DateTimeOffset? RevokedAt);

    private sealed record CreatedKey(KeyInfo Key, string Secret);

    private sealed record Summary(Guid Id, string Reference, string Status);

    private sealed record Page(Summary[] Items, int TotalCount);

    private sealed record Person(string FullName, string Email);

    private sealed record Comment(string AuthorName, string Body);

    private sealed record Details(Guid Id, string Reference, string Status, Person Requester, Comment[] Comments);

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = postgres.Api.CreateApiClient();
        return client.Authorize(await client.LoginAsync(email));
    }

    private HttpClient WithKey(string key)
    {
        var client = postgres.Api.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", key);
        return client;
    }

    private static async Task<CreatedKey> CreateKeyAsync(HttpClient admin, string name, params string[] scopes)
    {
        var response = await admin.PostAsJsonAsync("/api/integrations/api-keys", new { name, scopes });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedKey>())!;
    }

    [Fact]
    public async Task A_key_is_shown_once_reads_its_organisation_and_stops_working_when_revoked()
    {
        var admin = await SignedInAs(DemoUsers.AcmeHrAdmin);
        var created = await CreateKeyAsync(admin, "HRIS sync", "tickets:read");
        created.Secret.Should().StartWith($"hrd_{created.Key.Prefix}_");

        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var ticket = await employee.SubmitWorkCertificateAsync("Integration read");

        var integration = WithKey(created.Secret);
        var page = await integration.GetFromJsonAsync<Page>("/api/integration/v1/tickets?pageSize=200");
        page!.Items.Should().Contain(t => t.Id == ticket.Id);
        var details = await integration.GetFromJsonAsync<Details>($"/api/integration/v1/tickets/{ticket.Id}");
        details!.Requester.Email.Should().Be(DemoUsers.AcmeEmployee);

        var keys = await admin.GetFromJsonAsync<KeyInfo[]>("/api/integrations/api-keys");
        keys.Should().Contain(k => k.Id == created.Key.Id && k.LastUsedAt != null);
        (await admin.GetStringAsync("/api/integrations/api-keys")).Should().NotContain(created.Secret);

        (await admin.DeleteAsync($"/api/integrations/api-keys/{created.Key.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await integration.GetAsync("/api/integration/v1/tickets")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Integration_endpoints_accept_only_valid_keys_and_keys_reach_nothing_else()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        (await employee.GetAsync("/api/integration/v1/tickets")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await WithKey("hrd_abcdefgh_not-a-real-key-at-all").GetAsync("/api/integration/v1/tickets")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await WithKey("garbage").GetAsync("/api/integration/v1/tickets")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var created = await CreateKeyAsync(await SignedInAs(DemoUsers.AcmeHrAdmin), "Scoped", "tickets:read", "tickets:write");
        (await WithKey(created.Secret).GetAsync("/api/tickets/mine")).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "a key is not a user session");
    }

    [Fact]
    public async Task Keys_see_their_organisation_only_and_never_confidential_cases()
    {
        var globexKey = await CreateKeyAsync(await SignedInAs(DemoUsers.GlobexHrAdmin), "Globex HRIS", "tickets:read");
        var acmeKey = await CreateKeyAsync(await SignedInAs(DemoUsers.AcmeHrAdmin), "Acme HRIS", "tickets:read");
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var ticket = await employee.SubmitWorkCertificateAsync("Isolation check");
        var harassmentType = await employee.RequestTypeIdAsync("Harassment report");
        var confidential = await employee.SubmitAsync(harassmentType, "Confidential matter",
            new System.Text.Json.Nodes.JsonObject { ["incidentDate"] = "2026-03-01", ["whatHappened"] = "A detailed description of what happened." });
        var confidentialId = (await confidential.Content.ReadFromJsonAsync<Created>())!.Id;

        (await WithKey(globexKey.Secret).GetAsync($"/api/integration/v1/tickets/{ticket.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await WithKey(acmeKey.Secret).GetAsync($"/api/integration/v1/tickets/{confidentialId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var page = await WithKey(acmeKey.Secret).GetFromJsonAsync<Page>("/api/integration/v1/tickets?pageSize=200");
        page!.Items.Should().NotContain(t => t.Id == confidentialId);
    }

    [Fact]
    public async Task A_write_key_comments_and_resolves_as_its_service_account_while_a_read_key_cannot()
    {
        var admin = await SignedInAs(DemoUsers.AcmeHrAdmin);
        var reader = await CreateKeyAsync(admin, "Read only", "tickets:read");
        var writer = await CreateKeyAsync(admin, "Payroll bridge", "tickets:read", "tickets:write");
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var ticket = await employee.SubmitWorkCertificateAsync("Processed by payroll");

        (await WithKey(reader.Secret).PostAsJsonAsync($"/api/integration/v1/tickets/{ticket.Id}/comments", new { body = "Hi" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var integration = WithKey(writer.Secret);
        (await integration.PostAsJsonAsync($"/api/integration/v1/tickets/{ticket.Id}/comments", new { body = "Received by payroll." }))
            .EnsureSuccessStatusCode();
        (await integration.PostAsJsonAsync($"/api/integration/v1/tickets/{ticket.Id}/status", new { status = "Open" })).EnsureSuccessStatusCode();
        (await integration.PostAsJsonAsync($"/api/integration/v1/tickets/{ticket.Id}/status", new { status = "Resolved", reason = "Certificate generated." }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await integration.PostAsJsonAsync($"/api/integration/v1/tickets/{ticket.Id}/status", new { status = "Reopened" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "reopening is the employee's decision");

        var details = await integration.GetFromJsonAsync<Details>($"/api/integration/v1/tickets/{ticket.Id}");
        details!.Status.Should().Be("Resolved");
        details.Comments.Select(c => c.AuthorName).Should().AllBe("Payroll bridge (API)");
    }
}
