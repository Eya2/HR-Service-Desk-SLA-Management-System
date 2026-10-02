using System.Net;
using System.Net.Http.Json;
using HrServiceDesk.IntegrationTests.Infrastructure;

namespace HrServiceDesk.IntegrationTests.Knowledge;

[Collection(PostgresCollection.Name)]
public sealed class KnowledgeTests(PostgresFixture postgres)
{
    private sealed record Summary(Guid Id, string Title, bool IsPublished, int ViewCount, int HelpfulCount);

    private sealed record Article(Guid Id, string Title, string Body, string? Category, bool IsPublished, int ViewCount, int HelpfulCount);

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = postgres.Api.CreateApiClient();
        return client.Authorize(await client.LoginAsync(email));
    }

    private static async Task<Article> SaveAsync(HttpClient admin, string title, string body, bool publish, Guid? id = null)
    {
        var payload = new { title, summary = "Short summary.", body, category = "Payroll", isPublished = publish };
        var response = id is { } existing
            ? await admin.PutAsJsonAsync($"/api/knowledge/{existing}", payload)
            : await admin.PostAsJsonAsync("/api/knowledge", payload);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Article>())!;
    }

    [Fact]
    public async Task Search_is_accent_insensitive_matches_word_prefixes_and_ranks_titles_first()
    {
        var admin = await SignedInAs(DemoUsers.AcmeHrAdmin);
        var inBody = await SaveAsync(admin, "Zorblax travel rules", "Mention of quuxification in the body only.", publish: true);
        var inTitle = await SaveAsync(admin, "Quuxification expenses", "Everything about expense claims.", publish: true);
        await SaveAsync(admin, "Quuxification draft", "Not published yet.", publish: false);

        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var hits = await employee.GetFromJsonAsync<Summary[]>("/api/knowledge?q=" + Uri.EscapeDataString("QUUXIFICATIÔ"));

        hits!.Select(h => h.Id).Should().Equal(inTitle.Id, inBody.Id);
    }

    [Fact]
    public async Task Suggestions_return_at_most_five_published_articles_and_ignore_short_text()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);

        var suggestions = await employee.GetFromJsonAsync<Summary[]>("/api/knowledge/suggest?text=payslip%20salary%20certificate%20leave%20bank%20training");
        var tooShort = await employee.GetFromJsonAsync<Summary[]>("/api/knowledge/suggest?text=pa");

        suggestions.Should().NotBeEmpty().And.HaveCountLessThanOrEqualTo(5).And.OnlyContain(s => s.IsPublished);
        tooShort.Should().BeEmpty();
    }

    [Fact]
    public async Task Articles_are_isolated_per_organisation()
    {
        var acme = await SignedInAs(DemoUsers.AcmeHrAdmin);
        var article = await SaveAsync(acme, "Flibbertigibbet allowance", "Acme only.", publish: true);

        var globex = await SignedInAs(DemoUsers.GlobexEmployee);

        (await globex.GetAsync($"/api/knowledge/{article.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await globex.GetFromJsonAsync<Summary[]>("/api/knowledge?q=flibbertigibbet")).Should().BeEmpty();
    }

    [Fact]
    public async Task Reading_and_helpful_votes_are_counted_and_drafts_are_hidden_from_employees()
    {
        var admin = await SignedInAs(DemoUsers.AcmeHrAdmin);
        var draft = await SaveAsync(admin, "Snorkelwacker policy", "Draft.", publish: false);
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);

        (await employee.GetAsync($"/api/knowledge/{draft.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await employee.PostAsync($"/api/knowledge/{draft.Id}/helpful", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await employee.GetFromJsonAsync<Summary[]>("/api/knowledge")).Should().NotContain(s => s.Id == draft.Id);
        (await admin.GetFromJsonAsync<Summary[]>("/api/knowledge?includeUnpublished=true")).Should().Contain(s => s.Id == draft.Id);

        await SaveAsync(admin, "Snorkelwacker policy", "Now live.", publish: true, draft.Id);
        await employee.GetFromJsonAsync<Article>($"/api/knowledge/{draft.Id}");
        (await employee.PostAsync($"/api/knowledge/{draft.Id}/helpful", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var read = await admin.GetFromJsonAsync<Article>($"/api/knowledge/{draft.Id}");
        read!.Body.Should().Be("Now live.");
        read.ViewCount.Should().Be(2);
        read.HelpfulCount.Should().Be(1);
    }

    [Fact]
    public async Task Only_hr_admins_write_articles_and_input_is_validated()
    {
        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        var admin = await SignedInAs(DemoUsers.AcmeHrAdmin);
        var payload = new { title = "Mine", body = "Body", isPublished = true };

        (await employee.PostAsJsonAsync("/api/knowledge", payload)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsJsonAsync("/api/knowledge", new { title = "", body = "", category = "Nope", isPublished = true }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
