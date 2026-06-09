using FleetVision.FleetAssets.Domain.Entities;
using FleetVision.FleetAssets.Domain.Enums;
using FleetVision.FleetAssets.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace FleetVision.FleetAssets.Domain.Tests.Entities;

public sealed class VehicleTests
{
    private static Vehicle Valid() =>
        Vehicle.Create(Guid.NewGuid(), Guid.NewGuid(), "ABC-123", null, "Toyota", "Hilux", 2022);

    [Fact]
    public void Create_WithValidData_ShouldCreateActiveVehicle()
    {
        var tenantId = Guid.NewGuid();
        var fleetId  = Guid.NewGuid();

        var vehicle = Vehicle.Create(tenantId, fleetId, "ABC-123", "1HGBH41JXMN109186",
            "Toyota", "Hilux", 2022, 5000);

        vehicle.TenantId.Should().Be(tenantId);
        vehicle.FleetId.Should().Be(fleetId);
        vehicle.Plate.Should().Be("ABC-123");
        vehicle.Vin.Should().Be("1HGBH41JXMN109186");
        vehicle.Brand.Should().Be("Toyota");
        vehicle.Model.Should().Be("Hilux");
        vehicle.Year.Should().Be(2022);
        vehicle.OdometerKm.Should().Be(5000);
        vehicle.Status.Should().Be(VehicleStatus.Active);
        vehicle.IsDeleted.Should().BeFalse();
        vehicle.LastKnownPosition.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldNormalizePlateToUppercase()
    {
        var vehicle = Vehicle.Create(Guid.NewGuid(), Guid.NewGuid(), "abc-123", null, "Brand", "Model", 2020);
        vehicle.Plate.Should().Be("ABC-123");
    }

    [Fact]
    public void Create_WithYear1899_ShouldThrow()
    {
        var act = () => Vehicle.Create(Guid.NewGuid(), Guid.NewGuid(), "ABC", null, "Brand", "Model", 1899);
        act.Should().Throw<ArgumentException>().WithMessage("*Year*");
    }

    [Fact]
    public void Create_WithNegativeOdometer_ShouldThrow()
    {
        var act = () => Vehicle.Create(Guid.NewGuid(), Guid.NewGuid(), "ABC", null, "Brand", "Model", 2020, -1);
        act.Should().Throw<ArgumentException>().WithMessage("*Odometer*");
    }

    [Fact]
    public void UpdatePosition_WithValidCoords_ShouldSetPoint()
    {
        var vehicle = Valid();

        vehicle.UpdatePosition(-74.0060, 40.7128);

        vehicle.LastKnownPosition.Should().NotBeNull();
        vehicle.LastKnownPosition!.X.Should().BeApproximately(-74.0060, 0.0001); // longitude
        vehicle.LastKnownPosition!.Y.Should().BeApproximately(40.7128, 0.0001);  // latitude
        vehicle.LastKnownPosition!.SRID.Should().Be(4326);
    }

    [Theory]
    [InlineData(-181, 0)]
    [InlineData(181, 0)]
    public void UpdatePosition_WithInvalidLongitude_ShouldThrow(double lon, double lat)
    {
        var vehicle = Valid();
        var act = () => vehicle.UpdatePosition(lon, lat);
        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*longitude*");
    }

    [Theory]
    [InlineData(0, -91)]
    [InlineData(0, 91)]
    public void UpdatePosition_WithInvalidLatitude_ShouldThrow(double lon, double lat)
    {
        var vehicle = Valid();
        var act = () => vehicle.UpdatePosition(lon, lat);
        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*latitude*");
    }

    [Fact]
    public void SoftDelete_ShouldSetIsDeletedTrue()
    {
        var vehicle = Valid();
        vehicle.SoftDelete();
        vehicle.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void SoftDelete_WhenAlreadyDeleted_ShouldThrow()
    {
        var vehicle = Valid();
        vehicle.SoftDelete();
        var act = () => vehicle.SoftDelete();
        act.Should().Throw<VehicleAlreadyDeletedException>();
    }

    [Fact]
    public void Update_ShouldChangeStatus()
    {
        var vehicle = Valid();
        vehicle.Update("XYZ-999", "Ford", "Ranger", 2021, 10000, VehicleStatus.Maintenance);
        vehicle.Status.Should().Be(VehicleStatus.Maintenance);
        vehicle.OdometerKm.Should().Be(10000);
    }
}
