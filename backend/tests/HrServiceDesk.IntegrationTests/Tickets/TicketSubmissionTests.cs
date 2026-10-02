using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HrServiceDesk.IntegrationTests.Infrastructure;

namespace HrServiceDesk.IntegrationTests.Tickets;

[Collection(PostgresCollection.Name)]
public sealed partial class TicketSubmissionTests(PostgresFixture postgres)
{
    private readonly ApiFactory _api = postgres.Api;

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = _api.CreateApiClient();
        return client.Authorize(await client.LoginAsync(email));
    }

    [Fact]
    public async Task Employee_submits_a_payslip_correction_with_its_document()
    {
        var client = await SignedInAs(DemoUsers.AcmeEmployee);

        var created = await client.SubmitPayslipCorrectionAsync();

        created.Reference.Should().MatchRegex(@"^HR-20\d\d-\d{6}$");
        var ticket = (await client.GetFromJsonAsync<TicketDetails>($"/api/tickets/{created.Id}"))!;
        ticket.Status.Should().Be("PendingApproval", "payslip corrections need the manager's approval");
        ticket.Priority.Should().Be("High");
        ticket.RequestTypeName.Should().Be("Payslip correction");
        ticket.Answers.Select(a => a.Label).Should().Equal("Pay period", "Issue", "Expected amount", "Payslip");
        var issue = ticket.Answers.Single(a => a.Key == "issue");
        issue.Value!.GetValue<string>().Should().Be("missing_overtime");
        issue.DisplayValue.Should().Be("Missing overtime");
        ticket.Answers.Single(a => a.Key == "expectedAmount").DisplayValue.Should().Be("320.5");
        var payslip = ticket.Answers.Single(a => a.Key == "payslip").Files.Should().ContainSingle().Subject;
        payslip.FileName.Should().Be("payslip-march.pdf");
        payslip.ContentType.Should().Be("application/pdf");
        ticket.Permissions.Should().BeEquivalentTo(new Permissions(true, false, false, false, true, ["Cancelled"]), "a case pending approval can no longer be reworded");
        ticket.Timeline.Select(e => e.Type).Should().Equal("Created", "StatusChanged", "ApprovalRequested");
    }

    [Fact]
    public async Task Attachment_downloads_return_the_original_bytes_as_a_download()
    {
        var client = await SignedInAs(DemoUsers.AcmeEmployee);
        var created = await client.SubmitPayslipCorrectionAsync();
        var ticket = (await client.GetFromJsonAsync<TicketDetails>($"/api/tickets/{created.Id}"))!;

        var response = await client.GetAsync($"/api/tickets/{created.Id}/attachments/{ticket.Attachments[0].Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(TicketApi.Pdf);
    }

    [Fact]
    public async Task Form_errors_are_reported_per_field()
    {
        var client = await SignedInAs(DemoUsers.AcmeEmployee);
        var typeId = await client.RequestTypeIdAsync("Payslip correction");

        var response = await client.SubmitAsync(typeId, "Bad", new JsonObject { ["payPeriod"] = "March", ["issue"] = "nope" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = await response.ValidationErrorsAsync();
        errors.Keys.Should().BeEquivalentTo("values.payPeriod", "values.issue", "values.payslip");
        errors["values.payslip"].Should().ContainSingle().Which.Should().Be("Payslip is required.");
    }

    [Fact]
    public async Task Disguised_files_are_rejected_and_nothing_is_created()
    {
        var client = await SignedInAs(DemoUsers.AcmeEmployee);
        var typeId = await client.RequestTypeIdAsync("Payslip correction");
        var before = (await client.GetFromJsonAsync<TicketPage>("/api/tickets/mine"))!.TotalCount;

        var response = await client.SubmitAsync(
            typeId, "Malware", new JsonObject { ["payPeriod"] = "2026-03", ["issue"] = "other" },
            new TestFile("payslip", "payslip.pdf", TicketApi.Exe));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ValidationErrorsAsync())["values.payslip"].Single().Should().Contain("does not match its extension");
        (await client.GetFromJsonAsync<TicketPage>("/api/tickets/mine"))!.TotalCount.Should().Be(before);
    }

    [Fact]
    public async Task Malformed_payload_and_unknown_type_are_rejected()
    {
        var client = await SignedInAs(DemoUsers.AcmeEmployee);

        (await client.SubmitRawAsync("not json")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.SubmitAsync(Guid.NewGuid(), "Ghost", [])).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Request_types_of_another_organisation_cannot_be_used()
    {
        var acme = await SignedInAs(DemoUsers.AcmeEmployee);
        var acmeType = await acme.RequestTypeIdAsync("Work certificate");
        var globex = await SignedInAs(DemoUsers.GlobexEmployee);

        var response = await globex.SubmitAsync(acmeType, "Cross-tenant", new JsonObject { ["purpose"] = "bank", ["language"] = "fr", ["copies"] = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Concurrent_submissions_get_unique_consecutive_references()
    {
        var client = await SignedInAs(DemoUsers.AcmeManager);
        var typeId = await client.RequestTypeIdAsync("Work certificate");
        var values = new JsonObject { ["purpose"] = "bank", ["language"] = "fr", ["copies"] = 1 };

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            client.SubmitAsync(typeId, $"Certificate {i}", (JsonObject)values.DeepClone())));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created);
        var numbers = new List<int>();
        foreach (var response in responses)
        {
            var reference = (await response.Content.ReadFromJsonAsync<Created>())!.Reference;
            numbers.Add(int.Parse(SequencePattern().Match(reference).Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        numbers.Should().OnlyHaveUniqueItems();
        (numbers.Max() - numbers.Min()).Should().Be(7, "references are allocated without gaps");
    }

    [GeneratedRegex(@"-(\d{6})$")]
    private static partial Regex SequencePattern();
}
