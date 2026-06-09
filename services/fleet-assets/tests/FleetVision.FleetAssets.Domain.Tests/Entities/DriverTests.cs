using FleetVision.FleetAssets.Domain.Entities;
using FleetVision.FleetAssets.Domain.Enums;
using FleetVision.FleetAssets.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace FleetVision.FleetAssets.Domain.Tests.Entities;

public sealed class DriverTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateActiveDriver()
    {
        var tenantId = Guid.NewGuid();

        var driver = Driver.Create(tenantId, "Juan Pérez", "DL-001234", "+57-300-000-0000", "juan@corp.com");

        driver.TenantId.Should().Be(tenantId);
        driver.FullName.Should().Be("Juan Pérez");
        driver.LicenseNumber.Should().Be("DL-001234");
        driver.Phone.Should().Be("+57-300-000-0000");
        driver.Email.Should().Be("juan@corp.com");
        driver.Status.Should().Be(DriverStatus.Active);
        driver.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldNormalizeEmailToLowercase()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Name", "LIC", null, "Juan@Corp.COM");
        driver.Email.Should().Be("juan@corp.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyFullName_ShouldThrow(string name)
    {
        var act = () => Driver.Create(Guid.NewGuid(), name, "LIC");
        act.Should().Throw<ArgumentException>().WithMessage("*FullName*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyLicenseNumber_ShouldThrow(string license)
    {
        var act = () => Driver.Create(Guid.NewGuid(), "Name", license);
        act.Should().Throw<ArgumentException>().WithMessage("*LicenseNumber*");
    }

    [Fact]
    public void SoftDelete_ShouldSetIsDeletedTrue()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Name", "LIC");
        driver.SoftDelete();
        driver.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void SoftDelete_WhenAlreadyDeleted_ShouldThrow()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Name", "LIC");
        driver.SoftDelete();
        var act = () => driver.SoftDelete();
        act.Should().Throw<DriverAlreadyDeletedException>();
    }

    [Fact]
    public void Update_ShouldChangeStatusAndName()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Old Name", "LIC");
        driver.Update("New Name", "LIC-NEW", null, null, DriverStatus.Inactive);
        driver.FullName.Should().Be("New Name");
        driver.Status.Should().Be(DriverStatus.Inactive);
    }
}
