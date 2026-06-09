using FleetVision.TenantManagement.Domain.Entities;
using FleetVision.TenantManagement.Domain.Enums;
using FleetVision.TenantManagement.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace FleetVision.TenantManagement.Domain.Tests.Entities;

public sealed class TenantProfileTests
{
    private static TenantProfile ValidProfile(PlanTier plan = PlanTier.Free) =>
        TenantProfile.Create(Guid.NewGuid(), "Acme Corp", "acme-corp", "billing@acme.com", plan);

    [Fact]
    public void Create_WithValidData_ShouldCreateActiveProfile()
    {
        var tenantId = Guid.NewGuid();
        var profile  = TenantProfile.Create(tenantId, "Acme Corp", "acme-corp", "billing@acme.com");

        profile.Id.Should().NotBeEmpty();
        profile.TenantId.Should().Be(tenantId);
        profile.CompanyName.Should().Be("Acme Corp");
        profile.Slug.Should().Be("acme-corp");
        profile.Plan.Should().Be(PlanTier.Free);
        profile.IsActive.Should().BeTrue();
        profile.MaxVehicles.Should().Be(3);
        profile.MaxUsers.Should().Be(5);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyCompanyName_ShouldThrow(string name)
    {
        var act = () => TenantProfile.Create(Guid.NewGuid(), name, "slug", "b@a.com");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptySlug_ShouldThrow(string slug)
    {
        var act = () => TenantProfile.Create(Guid.NewGuid(), "Name", slug, "b@a.com");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldNormalizeSlugToLowercase()
    {
        var profile = TenantProfile.Create(Guid.NewGuid(), "Name", "ACME-CORP", "b@a.com");
        profile.Slug.Should().Be("acme-corp");
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrow()
    {
        var act = () => TenantProfile.Create(Guid.Empty, "Name", "slug", "b@a.com");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ChangePlan_ToHigherTier_ShouldUpdateLimits()
    {
        var profile = ValidProfile(PlanTier.Starter);

        profile.ChangePlan(PlanTier.Professional);

        profile.Plan.Should().Be(PlanTier.Professional);
        profile.MaxVehicles.Should().Be(100);
        profile.MaxUsers.Should().Be(100);
    }

    [Fact]
    public void ChangePlan_ToSameTier_ShouldSucceed()
    {
        var profile = ValidProfile(PlanTier.Starter);
        var act = () => profile.ChangePlan(PlanTier.Starter);
        act.Should().NotThrow();
    }

    [Fact]
    public void ChangePlan_Downgrade_ShouldThrowPlanDowngradeNotAllowedException()
    {
        var profile = ValidProfile(PlanTier.Professional);

        var act = () => profile.ChangePlan(PlanTier.Starter);

        act.Should().Throw<PlanDowngradeNotAllowedException>();
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveToFalse()
    {
        var profile = ValidProfile();
        profile.Deactivate();
        profile.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_ShouldSetIsActiveToTrue()
    {
        var profile = ValidProfile();
        profile.Deactivate();
        profile.Activate();
        profile.IsActive.Should().BeTrue();
    }

    [Fact]
    public void ChangePlan_ShouldUpdateUpdatedAt()
    {
        var profile = ValidProfile(PlanTier.Free);
        var before  = profile.UpdatedAt;

        profile.ChangePlan(PlanTier.Starter);

        profile.UpdatedAt.Should().BeOnOrAfter(before);
    }
}
