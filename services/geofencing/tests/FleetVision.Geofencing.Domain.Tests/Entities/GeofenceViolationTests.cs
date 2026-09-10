using FleetVision.Geofencing.Domain.Entities;
using FleetVision.Geofencing.Domain.Enums;
using FluentAssertions;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Xunit;

namespace FleetVision.Geofencing.Domain.Tests.Entities;

public sealed class GeofenceViolationTests
{
    private static readonly GeometryFactory Factory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    private static Point ValidPosition() => Factory.CreatePoint(new Coordinate(-74.005, 40.715));

    [Fact]
    public void Create_WithValidData_ShouldPopulateAllFields()
    {
        var tenantId   = Guid.NewGuid();
        var geofenceId = Guid.NewGuid();
        var vehicleId  = Guid.NewGuid();
        var driverId   = Guid.NewGuid();
        var position   = ValidPosition();

        var violation = GeofenceViolation.Create(
            tenantId, geofenceId, vehicleId, driverId,
            ViolationType.SpeedExceeded, position, actualSpeedKmh: 90, limitSpeedKmh: 60);

        violation.Id.Should().NotBeEmpty();
        violation.TenantId.Should().Be(tenantId);
        violation.GeofenceId.Should().Be(geofenceId);
        violation.VehicleId.Should().Be(vehicleId);
        violation.DriverId.Should().Be(driverId);
        violation.ViolationType.Should().Be(ViolationType.SpeedExceeded);
        violation.Position.Should().BeSameAs(position);
        violation.ActualSpeedKmh.Should().Be(90);
        violation.LimitSpeedKmh.Should().Be(60);
        violation.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithNullDriverAndSpeeds_ShouldBeAllowed()
    {
        var violation = GeofenceViolation.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            ViolationType.ZoneEntered, ValidPosition());

        violation.DriverId.Should().BeNull();
        violation.ActualSpeedKmh.Should().BeNull();
        violation.LimitSpeedKmh.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrowArgumentException()
    {
        var act = () => GeofenceViolation.Create(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), null,
            ViolationType.ZoneEntered, ValidPosition());

        act.Should().Throw<ArgumentException>().WithParameterName("tenantId");
    }

    [Fact]
    public void Create_WithEmptyGeofenceId_ShouldThrowArgumentException()
    {
        var act = () => GeofenceViolation.Create(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), null,
            ViolationType.ZoneEntered, ValidPosition());

        act.Should().Throw<ArgumentException>().WithParameterName("geofenceId");
    }

    [Fact]
    public void Create_WithEmptyVehicleId_ShouldThrowArgumentException()
    {
        var act = () => GeofenceViolation.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, null,
            ViolationType.ZoneEntered, ValidPosition());

        act.Should().Throw<ArgumentException>().WithParameterName("vehicleId");
    }

    [Fact]
    public void Create_WithNullPosition_ShouldThrowArgumentNullException()
    {
        var act = () => GeofenceViolation.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            ViolationType.ZoneEntered, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("position");
    }

    [Fact]
    public void Create_ShouldGenerateUniqueIds()
    {
        var a = GeofenceViolation.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            ViolationType.ZoneEntered, ValidPosition());
        var b = GeofenceViolation.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            ViolationType.ZoneEntered, ValidPosition());

        a.Id.Should().NotBe(b.Id);
    }
}
