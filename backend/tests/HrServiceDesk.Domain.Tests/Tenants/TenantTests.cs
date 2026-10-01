using HrServiceDesk.Domain.Common;
using HrServiceDesk.Domain.Tenants;

namespace HrServiceDesk.Domain.Tests.Tenants;

public class TenantTests
{
    [Fact]
    public void Create_normalizes_name_and_slug()
    {
        var tenant = Tenant.Create("  Acme Tunisie ", " ACME-TN ", "Africa/Tunis", "fr");

        tenant.Name.Should().Be("Acme Tunisie");
        tenant.Slug.Should().Be("acme-tn");
        tenant.TimeZoneId.Should().Be("Africa/Tunis");
        tenant.IsActive.Should().BeTrue();
        tenant.Id.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("acme tn")]
    [InlineData("-acme")]
    [InlineData("acme--tn")]
    [InlineData("acme_tn")]
    public void Create_rejects_invalid_slug(string slug)
    {
        var act = () => Tenant.Create("Acme", slug, "Africa/Tunis", "fr");

        act.Should().Throw<DomainException>().Which.Code.Should().Be("tenant.invalid_slug");
    }

    [Fact]
    public void Create_rejects_empty_name()
    {
        var act = () => Tenant.Create("   ", "acme", "Africa/Tunis", "fr");

        act.Should().Throw<DomainException>().Which.Code.Should().Be("tenant.invalid_name");
    }

    [Fact]
    public void Create_defaults_culture_to_french()
    {
        Tenant.Create("Acme", "acme", "Europe/Paris", " ").DefaultCulture.Should().Be("fr");
    }

    [Fact]
    public void Deactivate_then_activate_toggles_state()
    {
        var tenant = Tenant.Create("Acme", "acme", "Europe/Paris", "fr");

        tenant.Deactivate();
        tenant.IsActive.Should().BeFalse();

        tenant.Activate();
        tenant.IsActive.Should().BeTrue();
    }
}
