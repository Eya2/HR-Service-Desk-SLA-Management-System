using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using HrServiceDesk.IntegrationTests.Infrastructure;

namespace HrServiceDesk.IntegrationTests.Assistant;

[Collection(PostgresCollection.Name)]
public sealed class AssistantTests(PostgresFixture postgres)
{
    private sealed record Suggestion(Guid RequestTypeId, string Name, string Category, bool IsConfidential, double Confidence, string Title, JsonObject Values, string? Reason);

    private sealed record Option(Guid Id, string Name, string Category);

    private sealed record ArticleRef(Guid Id, string Title);

    private sealed record Classification(Suggestion? Suggestion, Option[] Alternatives, ArticleRef[] Articles, string Source);

    private sealed record Draft(string Body, string Source, ArticleRef[] Articles);

    private static async Task<HttpClient> SignedInAs(ApiFactory api, string email)
    {
        var client = api.CreateApiClient();
        return client.Authorize(await client.LoginAsync(email));
    }

    private static async Task<Classification> ClassifyAsync(HttpClient client, string text)
    {
        var response = await client.PostAsJsonAsync("/api/assistant/classify", new { text });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Classification>())!;
    }

    [Fact]
    public async Task Without_a_key_the_local_classifier_routes_a_french_request_and_prefills_the_form()
    {
        var employee = await SignedInAs(postgres.Api, DemoUsers.AcmeEmployee);

        var result = await ClassifyAsync(employee, "Mes heures sup de mars 2026 n'apparaissent pas sur ma fiche de paie. Merci de corriger.");

        result.Source.Should().Be("local");
        result.Suggestion!.Name.Should().Be("Payslip correction");
        result.Suggestion.Title.Should().Be("Mes heures sup de mars 2026 n'apparaissent pas sur ma fiche de paie.");
        result.Suggestion.Values["issue"]!.GetValue<string>().Should().Be("missing_overtime");
        result.Suggestion.Values["payPeriod"]!.GetValue<string>().Should().Be("2026-03");
        result.Suggestion.Values.Should().NotContainKey("payslip");
    }

    [Theory]
    [InlineData("I need an employment certificate for my visa application at the embassy", "Work certificate")]
    [InlineData("Je change de banque, voici mon nouveau RIB", "Change of bank details")]
    [InlineData("Je suis malade depuis lundi, je voudrais poser un congé maladie", "Leave request")]
    [InlineData("Je voudrais une avance sur salaire de 800 dinars", "Salary advance")]
    [InlineData("I would like to attend an AWS certification course", "Training request")]
    public async Task The_local_classifier_understands_everyday_french_and_english(string text, string expected)
    {
        var employee = await SignedInAs(postgres.Api, DemoUsers.AcmeEmployee);

        (await ClassifyAsync(employee, text)).Suggestion!.Name.Should().Be(expected);
    }

    [Fact]
    public async Task Text_with_no_match_gets_no_suggestion_and_empty_text_is_rejected()
    {
        var employee = await SignedInAs(postgres.Api, DemoUsers.AcmeEmployee);

        (await ClassifyAsync(employee, "zzz qqq xyzzy")).Suggestion.Should().BeNull();
        (await employee.PostAsJsonAsync("/api/assistant/classify", new { text = "" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Claude_routes_the_request_and_its_answers_are_checked_against_the_form()
    {
        await using var api = new AiApiFactory(postgres.ConnectionString);
        var employee = await SignedInAs(api, DemoUsers.AcmeEmployee);
        var payslip = await employee.RequestTypeIdAsync("Payslip correction");
        api.Claude.Respond = _ => FakeClaude.ToolUse("route_request", new JsonObject
        {
            ["request_type_id"] = payslip.ToString(),
            ["title"] = "Heures supplémentaires de mars manquantes",
            ["confidence"] = 0.93,
            ["reason"] = "Le salarié signale des heures manquantes sur sa fiche de paie.",
            ["values"] = new JsonObject
            {
                ["issue"] = "missing_overtime",
                ["payPeriod"] = "2026-03",
                ["expectedAmount"] = "not a number",
                ["payslip"] = "file.pdf",
                ["unknown"] = "x",
            },
        });

        var result = await ClassifyAsync(employee, "Il manque mes heures sup de mars sur ma paie");

        result.Source.Should().Be("claude");
        result.Suggestion!.RequestTypeId.Should().Be(payslip);
        result.Suggestion.Title.Should().Be("Heures supplémentaires de mars manquantes");
        result.Suggestion.Confidence.Should().Be(0.93);
        result.Suggestion.Values.Select(v => v.Key).Should().BeEquivalentTo("issue", "payPeriod");

        var call = api.Claude.Calls.Should().ContainSingle().Subject;
        call.ApiKey.Should().Be("test-anthropic-key");
        call.Version.Should().Be("2023-06-01");
        call.Body["model"]!.GetValue<string>().Should().Be("claude-haiku-4-5-20251001");
        call.Body["tool_choice"]!["name"]!.GetValue<string>().Should().Be("route_request");
        call.Body.ToJsonString().Should().NotContain("Harassment report", "confidential types are kept out of the catalog sent to the model")
            .And.Contain("Il manque mes heures sup");
    }

    [Fact]
    public async Task An_unknown_answer_or_an_unavailable_api_falls_back_to_the_local_classifier()
    {
        await using var api = new AiApiFactory(postgres.ConnectionString);
        var employee = await SignedInAs(api, DemoUsers.AcmeEmployee);

        api.Claude.Respond = _ => FakeClaude.ToolUse("route_request", new JsonObject { ["request_type_id"] = Guid.NewGuid().ToString(), ["title"] = "?", ["confidence"] = 1 });
        var invented = await ClassifyAsync(employee, "Je voudrais une attestation de travail pour mon visa");

        api.Claude.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var down = await ClassifyAsync(employee, "Je voudrais une attestation de travail pour mon visa");

        invented.Source.Should().Be("local");
        invented.Suggestion!.Name.Should().Be("Work certificate");
        down.Source.Should().Be("local");
        down.Suggestion!.Name.Should().Be("Work certificate");
    }

    [Fact]
    public async Task A_confidential_matter_is_never_sent_to_the_model()
    {
        await using var api = new AiApiFactory(postgres.ConnectionString);
        var employee = await SignedInAs(api, DemoUsers.AcmeEmployee);

        var result = await ClassifyAsync(employee, "Mon manager me fait des remarques humiliantes, c'est du harcèlement");

        result.Source.Should().Be("local");
        result.Suggestion!.Name.Should().Be("Harassment report");
        result.Suggestion.IsConfidential.Should().BeTrue();
        api.Claude.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Without_a_key_HR_gets_a_template_reply_in_the_employee_language()
    {
        var employee = await SignedInAs(postgres.Api, DemoUsers.AcmeEmployee);
        var officer = await SignedInAs(postgres.Api, DemoUsers.AcmeHrOfficer);
        var ticket = await employee.SubmitWorkCertificateAsync("Certificate for my bank loan");

        var response = await officer.PostAsync($"/api/tickets/{ticket.Id}/draft-reply", null);

        response.EnsureSuccessStatusCode();
        var draft = (await response.Content.ReadFromJsonAsync<Draft>())!;
        draft.Source.Should().Be("local");
        draft.Body.Should().StartWith("Hello Amira,").And.Contain("Certificate for my bank loan").And.EndWith("Leila");
    }

    [Fact]
    public async Task Claude_drafts_from_the_public_conversation_only()
    {
        await using var api = new AiApiFactory(postgres.ConnectionString);
        var employee = await SignedInAs(api, DemoUsers.AcmeEmployee);
        var officer = await SignedInAs(api, DemoUsers.AcmeHrOfficer);
        var ticket = await employee.SubmitWorkCertificateAsync("Attestation pour ma banque");
        (await employee.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new { body = "C'est urgent, mon rendez-vous est jeudi.", isInternal = false })).EnsureSuccessStatusCode();
        (await officer.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new { body = "Internal: check the contract end date first.", isInternal = true })).EnsureSuccessStatusCode();
        api.Claude.Respond = _ => FakeClaude.Text("Bonjour Amira,\n\nVotre attestation sera prête mercredi.\n\nCordialement,\nLeila");

        var response = await officer.PostAsync($"/api/tickets/{ticket.Id}/draft-reply", null);

        response.EnsureSuccessStatusCode();
        var draft = (await response.Content.ReadFromJsonAsync<Draft>())!;
        draft.Source.Should().Be("claude");
        draft.Body.Should().StartWith("Bonjour Amira");
        var sent = api.Claude.Calls.Should().ContainSingle().Subject.Body;
        sent["model"]!.GetValue<string>().Should().Be("claude-sonnet-5-5");
        sent.ToJsonString().Should().Contain("mon rendez-vous est jeudi").And.NotContain("check the contract end date");

        // Nothing is posted on the agent's behalf: the draft is only a proposal.
        var details = await officer.GetFromJsonAsync<TicketDetails>($"/api/tickets/{ticket.Id}");
        details!.Comments.Should().NotContain(c => c.Body.Contains("mercredi", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Confidential_and_sensitive_cases_and_employees_are_refused()
    {
        await using var api = new AiApiFactory(postgres.ConnectionString);
        var employee = await SignedInAs(api, DemoUsers.AcmeEmployee);
        var officer = await SignedInAs(api, DemoUsers.AcmeHrOfficer);
        var admin = await SignedInAs(api, DemoUsers.AcmeHrAdmin);

        var report = await employee.SubmitAsync(await employee.RequestTypeIdAsync("Harassment report"), "Report", new JsonObject
        {
            ["incidentDate"] = "2026-09-12",
            ["whatHappened"] = "Repeated inappropriate remarks during meetings.",
        });
        var reportId = (await report.Content.ReadFromJsonAsync<Created>())!.Id;
        var bank = await employee.SubmitAsync(await employee.RequestTypeIdAsync("Change of bank details"), "New bank account",
            new JsonObject { ["bankName"] = "BIAT", ["iban"] = "TN5910006035183598478831" },
            new TestFile("bankCertificate", "rib.pdf", TicketApi.Pdf));
        var bankId = (await bank.Content.ReadFromJsonAsync<Created>())!.Id;
        var certificate = await employee.SubmitWorkCertificateAsync();

        var confidential = await admin.PostAsync($"/api/tickets/{reportId}/draft-reply", null);
        var sensitive = await officer.PostAsync($"/api/tickets/{bankId}/draft-reply", null);
        var byEmployee = await employee.PostAsync($"/api/tickets/{certificate.Id}/draft-reply", null);

        (await officer.GetFromJsonAsync<TicketDetails>($"/api/tickets/{certificate.Id}"))!.Permissions.CanDraftWithAi.Should().BeTrue();
        (await officer.GetFromJsonAsync<TicketDetails>($"/api/tickets/{bankId}"))!.Permissions.CanDraftWithAi.Should().BeFalse();
        (await admin.GetFromJsonAsync<TicketDetails>($"/api/tickets/{reportId}"))!.Permissions.CanDraftWithAi.Should().BeFalse();
        (await employee.GetFromJsonAsync<TicketDetails>($"/api/tickets/{certificate.Id}"))!.Permissions.CanDraftWithAi.Should().BeFalse();

        confidential.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await confidential.ProblemCodeAsync()).Should().Be("ai.not_allowed");
        sensitive.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await sensitive.ProblemCodeAsync()).Should().Be("ai.not_allowed");
        byEmployee.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        api.Claude.Calls.Should().BeEmpty();
    }
}
