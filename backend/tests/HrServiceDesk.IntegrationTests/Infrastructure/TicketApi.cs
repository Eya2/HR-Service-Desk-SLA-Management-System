using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HrServiceDesk.IntegrationTests.Infrastructure;

public sealed record RequestTypeSummary(Guid Id, string Name, string Category, bool IsConfidential, bool IsActive);

public sealed record Created(Guid Id, string Reference);

public sealed record TestFile(string Field, string FileName, byte[] Content);

public sealed record Attachment(Guid Id, string FileName, string ContentType, long SizeBytes, string? FieldKey);

public sealed record Comment(Guid Id, string AuthorName, string Body, bool IsInternal);

public sealed record Answer(string Key, string Label, string Type, JsonNode? Value, string? DisplayValue, Attachment[] Files);

public sealed record Permissions(
    bool CanComment, bool CanCommentInternally, bool CanEdit, bool CanChangePriority, bool CanAttach, string[] AvailableTransitions,
    Guid? DecidableApprovalId = null);

public sealed record TimelineEntry(string Type, string? ActorName, JsonNode? Data);

public sealed record Approval(Guid Id, int StepOrder, string StepName, string ApproverRole, string? ApproverName, string Decision, string? DecidedByName, string? Comment);

public sealed record TicketDetails(
    Guid Id, string Reference, string Title, string Description, string RequestTypeName, string Status, string Priority,
    bool IsConfidential, Answer[] Answers, Attachment[] Attachments, Comment[] Comments, TimelineEntry[] Timeline, Approval[] Approvals, Permissions Permissions);

public sealed record TicketSummary(Guid Id, string Reference, string Title, string Status, string RequesterName);

public sealed record TicketPage(TicketSummary[] Items, int TotalCount);

public static class TicketApi
{
    public static readonly byte[] Pdf = [.. "%PDF-1.7\n% test document\n"u8];
    public static readonly byte[] Exe = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00];

    public static async Task<Guid> RequestTypeIdAsync(this HttpClient client, string name)
    {
        var types = await client.GetFromJsonAsync<RequestTypeSummary[]>("/api/request-types");
        return types!.Single(t => t.Name == name).Id;
    }

    public static async Task<HttpResponseMessage> SubmitAsync(
        this HttpClient client, Guid requestTypeId, string title, JsonObject values, params TestFile[] files)
    {
        var payload = new JsonObject
        {
            ["requestTypeId"] = requestTypeId.ToString(),
            ["title"] = title,
            ["description"] = "Submitted by an integration test.",
            ["values"] = values,
        };
        return await client.SubmitRawAsync(payload.ToJsonString(), files);
    }

    public static async Task<HttpResponseMessage> SubmitRawAsync(this HttpClient client, string payload, params TestFile[] files)
    {
        using var content = new MultipartFormDataContent { { new StringContent(payload), "payload" } };
        foreach (var file in files)
        {
            var part = new ByteArrayContent(file.Content);
            part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(part, file.Field, file.FileName);
        }

        return await client.PostAsync("/api/tickets", content);
    }

    /// <summary>A valid payslip correction with its mandatory payslip document.</summary>
    public static async Task<Created> SubmitPayslipCorrectionAsync(this HttpClient client, string title = "March payslip is missing overtime")
    {
        var typeId = await client.RequestTypeIdAsync("Payslip correction");
        var response = await client.SubmitAsync(
            typeId, title, new JsonObject { ["payPeriod"] = "2026-03", ["issue"] = "missing_overtime", ["expectedAmount"] = 320.5 },
            new TestFile("payslip", "payslip-march.pdf", Pdf));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Created>())!;
    }

    public static Task<HttpResponseMessage> ChangeStatusAsync(this HttpClient client, Guid ticketId, string status, string? reason = null) =>
        client.PostAsJsonAsync($"/api/tickets/{ticketId}/status", new { status, reason });

    /// <summary>A request type without approval workflow: the case starts as New.</summary>
    public static async Task<Created> SubmitWorkCertificateAsync(this HttpClient client, string title = "Certificate for my bank")
    {
        var typeId = await client.RequestTypeIdAsync("Work certificate");
        var response = await client.SubmitAsync(typeId, title, new JsonObject { ["purpose"] = "bank", ["language"] = "fr", ["copies"] = 1 });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Created>())!;
    }

    public static async Task<IReadOnlyDictionary<string, string[]>> ValidationErrorsAsync(this HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("errors").EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(e => e.GetString()!).ToArray());
    }
}
