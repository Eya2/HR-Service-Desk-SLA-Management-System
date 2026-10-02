using System.Net;
using System.Net.Http.Json;
using HrServiceDesk.IntegrationTests.Infrastructure;

namespace HrServiceDesk.IntegrationTests.Knowledge;

[Collection(PostgresCollection.Name)]
public sealed class SatisfactionTests(PostgresFixture postgres)
{
    private sealed record Rating(int Score, string? Comment);

    private sealed record Permissions(bool CanRate);

    private sealed record Kpis(double? AverageSatisfaction, int Ratings);

    private sealed record Board(Kpis Kpis);

    private sealed record Details(string Status, Rating? Satisfaction, Permissions Permissions);

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = postgres.Api.CreateApiClient();
        return client.Authorize(await client.LoginAsync(email));
    }

    [Fact]
    public async Task The_requester_rates_a_closed_case_once()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var officer = await SignedInAs(DemoUsers.AcmeHrAdmin);
        var ticket = await employee.SubmitWorkCertificateAsync("Rating flow");

        (await employee.PostAsJsonAsync($"/api/tickets/{ticket.Id}/satisfaction", new { score = 5 }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "the case is not closed yet");

        (await officer.ChangeStatusAsync(ticket.Id, "Open")).EnsureSuccessStatusCode();
        (await officer.ChangeStatusAsync(ticket.Id, "Resolved", "Sent.")).EnsureSuccessStatusCode();
        (await employee.ChangeStatusAsync(ticket.Id, "Closed")).EnsureSuccessStatusCode();

        (await employee.GetFromJsonAsync<Details>($"/api/tickets/{ticket.Id}"))!.Permissions.CanRate.Should().BeTrue();
        (await officer.PostAsJsonAsync($"/api/tickets/{ticket.Id}/satisfaction", new { score = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "only the requester rates");
        (await employee.PostAsJsonAsync($"/api/tickets/{ticket.Id}/satisfaction", new { score = 6 }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await employee.PostAsJsonAsync($"/api/tickets/{ticket.Id}/satisfaction", new { score = 4, comment = " Quick, thanks " }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await employee.PostAsJsonAsync($"/api/tickets/{ticket.Id}/satisfaction", new { score = 5 }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var details = await employee.GetFromJsonAsync<Details>($"/api/tickets/{ticket.Id}");
        details!.Satisfaction.Should().Be(new Rating(4, "Quick, thanks"));
        details.Permissions.CanRate.Should().BeFalse();

        var board = await officer.GetFromJsonAsync<Board>("/api/dashboard");
        board!.Kpis.Ratings.Should().BeGreaterThan(0);
        board.Kpis.AverageSatisfaction.Should().BeInRange(1, 5);
    }
}
