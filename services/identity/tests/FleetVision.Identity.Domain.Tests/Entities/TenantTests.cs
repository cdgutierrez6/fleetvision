using FleetVision.Identity.Domain.Entities;
using FluentAssertions;

namespace FleetVision.Identity.Domain.Tests.Entities;

public sealed class TenantTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateTenant()
    {
        var tenant = Tenant.Create("Acme Corp", "acme-corp");

        tenant.Id.Should().NotBeEmpty();
        tenant.Name.Should().Be("Acme Corp");
        tenant.Slug.Should().Be("acme-corp");
        tenant.IsActive.Should().BeTrue();
        tenant.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_ShouldThrow(string name)
    {
        var act = () => Tenant.Create(name, "valid-slug");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptySlug_ShouldThrow(string slug)
    {
        var act = () => Tenant.Create("Valid Name", slug);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldNormalizeSlugToLowercase()
    {
        var tenant = Tenant.Create("Acme Corp", "ACME-CORP");
        tenant.Slug.Should().Be("acme-corp");
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveToFalse()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.Deactivate();
        tenant.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_ShouldSetIsActiveToTrue()
    {
        var tenant = Tenant.Create("Test", "test");
        tenant.Deactivate();
        tenant.Activate();
        tenant.IsActive.Should().BeTrue();
    }
}
