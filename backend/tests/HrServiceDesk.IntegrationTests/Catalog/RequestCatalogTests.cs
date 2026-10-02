using System.Net;
using System.Net.Http.Json;
using HrServiceDesk.IntegrationTests.Infrastructure;

namespace HrServiceDesk.IntegrationTests.Catalog;

[Collection(PostgresCollection.Name)]
public sealed class RequestCatalogTests(PostgresFixture postgres)
{
    private readonly ApiFactory _api = postgres.Api;

    private async Task<HttpClient> SignedInAs(string email)
    {
        var client = _api.CreateApiClient();
        return client.Authorize(await client.LoginAsync(email));
    }

    private sealed record RequestTypeDetails(Guid Id, string Name, string Category, bool IsConfidential, string DefaultPriority, FieldDto[] Fields);

    private sealed record FieldDto(string Key, string Label, string Type, bool Required);

    [Fact]
    public async Task Employees_see_the_seeded_catalog()
    {
        var client = await SignedInAs(DemoUsers.AcmeEmployee);

        var types = await client.GetFromJsonAsync<RequestTypeSummary[]>("/api/request-types");

        types.Should().NotBeNull();
        types!.Select(t => t.Name).Should().Contain(
        [
            "Work certificate", "Payslip correction", "Leave request", "Salary advance",
            "Change of bank details", "Training request", "Harassment report",
        ]);
        types!.Single(t => t.Name == "Harassment report").IsConfidential.Should().BeTrue();
    }

    [Fact]
    public async Task Catalog_can_be_searched_and_filtered()
    {
        var client = await SignedInAs(DemoUsers.AcmeEmployee);

        var search = await client.GetFromJsonAsync<RequestTypeSummary[]>("/api/request-types?search=CERTIF");
        search!.Select(t => t.Name).Should().Contain("Work certificate");

        var payroll = await client.GetFromJsonAsync<RequestTypeSummary[]>("/api/request-types?category=Payroll");
        payroll!.Should().OnlyContain(t => t.Category == "Payroll").And.HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Request_type_exposes_its_form_definition()
    {
        var client = await SignedInAs(DemoUsers.AcmeEmployee);
        var id = await client.RequestTypeIdAsync("Payslip correction");

        var type = await client.GetFromJsonAsync<RequestTypeDetails>($"/api/request-types/{id}");

        type!.DefaultPriority.Should().Be("High");
        type.Fields.Select(f => f.Key).Should().Equal("payPeriod", "issue", "expectedAmount", "payslip");
        type.Fields.Single(f => f.Key == "payslip").Should().BeEquivalentTo(new { Type = "File", Required = true });
    }

    [Fact]
    public async Task Other_organisations_cannot_read_a_request_type()
    {
        var acme = await SignedInAs(DemoUsers.AcmeEmployee);
        var id = await acme.RequestTypeIdAsync("Work certificate");
        var globex = await SignedInAs(DemoUsers.GlobexEmployee);

        (await globex.GetAsync($"/api/request-types/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Only_hr_admins_can_maintain_the_catalog()
    {
        var client = await SignedInAs(DemoUsers.AcmeHrOfficer);

        var response = await client.PostAsJsonAsync("/api/request-types", NewType("Officer type"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Invalid_form_definitions_are_rejected_with_every_problem()
    {
        var client = await SignedInAs(DemoUsers.AcmeHrAdmin);
        var body = NewType($"Broken {Guid.NewGuid():N}") with
        {
            Fields =
            [
                new { key = "choice", label = "Choice", type = "Select", required = true },
                new { key = "choice", label = "Again", type = "Text", required = false },
            ],
        };

        var response = await client.PostAsJsonAsync("/api/request-types", body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.ProblemCodeAsync()).Should().Be("form_schema.invalid");
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("used more than once").And.Contain("needs at least one option");
    }

    [Fact]
    public async Task Admin_creates_then_retires_a_request_type()
    {
        var admin = await SignedInAs(DemoUsers.AcmeHrAdmin);
        var name = $"Parking badge {Guid.NewGuid():N}";

        var created = await admin.PostAsJsonAsync("/api/request-types", NewType(name));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var type = (await created.Content.ReadFromJsonAsync<RequestTypeDetails>())!;
        type.Fields.Should().ContainSingle(f => f.Key == "plate");

        var employee = await SignedInAs(DemoUsers.AcmeEmployee);
        (await employee.GetFromJsonAsync<RequestTypeSummary[]>("/api/request-types"))!.Should().Contain(t => t.Name == name);

        (await admin.PutAsJsonAsync($"/api/request-types/{type.Id}/active", new { isActive = false }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await employee.GetFromJsonAsync<RequestTypeSummary[]>("/api/request-types"))!.Should().NotContain(t => t.Name == name);
        (await employee.GetAsync($"/api/request-types/{type.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.GetFromJsonAsync<RequestTypeSummary[]>("/api/request-types?includeInactive=true"))!
            .Should().Contain(t => t.Name == name && !t.IsActive);
    }

    private sealed record SaveBody(string Name, string Description, string Category, bool IsConfidential, string DefaultPriority, object[] Fields);

    private static SaveBody NewType(string name) => new(
        name, "Request a parking badge.", "OnboardingOffboarding", false, "Low",
        [new { key = "plate", label = "Licence plate", type = "Text", required = true, maxLength = 15 }]);
}
