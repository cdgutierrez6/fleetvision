using FleetVision.Telemetry.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace FleetVision.Telemetry.Domain.Tests.Entities;

public sealed class VehiclePositionTests
{
    private static readonly Guid ValidVehicleId = Guid.NewGuid();
    private static readonly Guid ValidTenantId  = Guid.NewGuid();
    private static readonly DateTime ValidTime  = DateTime.UtcNow;

    [Fact]
    public void Create_WithValidData_ShouldCreatePosition()
    {
        var position = VehiclePosition.Create(
            ValidVehicleId, ValidTenantId, ValidTime, 40.715, -74.005);

        position.VehicleId.Should().Be(ValidVehicleId);
        position.TenantId.Should().Be(ValidTenantId);
        position.Latitude.Should().Be(40.715);
        position.Longitude.Should().Be(-74.005);
        position.Time.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Create_WithAllFields_ShouldSetAllProperties()
    {
        var driverId = Guid.NewGuid();
        var position = VehiclePosition.Create(
            ValidVehicleId, ValidTenantId, ValidTime,
            latitude: 40.715, longitude: -74.005,
            driverId: driverId,
            speedKmh: 60.0, headingDeg: 180,
            accuracyM: 5.0, hdop: 1.2,
            satelliteCount: 9, fuelPct: 75.0,
            engineOn: true, obd2Codes: new[] { "P0300" });

        position.DriverId.Should().Be(driverId);
        position.SpeedKmh.Should().Be(60.0);
        position.FuelPct.Should().Be(75.0);
        position.EngineOn.Should().BeTrue();
        position.Obd2Codes.Should().ContainSingle("P0300");
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(91)]
    public void Create_WithInvalidLatitude_ShouldThrow(double latitude)
    {
        var act = () => VehiclePosition.Create(ValidVehicleId, ValidTenantId, ValidTime, latitude, 0);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("latitude");
    }

    [Theory]
    [InlineData(-181)]
    [InlineData(181)]
    public void Create_WithInvalidLongitude_ShouldThrow(double longitude)
    {
        var act = () => VehiclePosition.Create(ValidVehicleId, ValidTenantId, ValidTime, 0, longitude);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("longitude");
    }

    [Fact]
    public void Create_WithEmptyVehicleId_ShouldThrow()
    {
        var act = () => VehiclePosition.Create(Guid.Empty, ValidTenantId, ValidTime, 40.0, -74.0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNegativeSpeed_ShouldThrow()
    {
        var act = () => VehiclePosition.Create(ValidVehicleId, ValidTenantId, ValidTime, 40.0, -74.0, speedKmh: -5.0);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("speedKmh");
    }

    [Fact]
    public void Create_WithFuelOver100_ShouldThrow()
    {
        var act = () => VehiclePosition.Create(ValidVehicleId, ValidTenantId, ValidTime, 40.0, -74.0, fuelPct: 101.0);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("fuelPct");
    }

    [Fact]
    public void Create_WithLocalDateTime_ShouldConvertToUtc()
    {
        var localTime = DateTime.Now; // DateTimeKind.Local
        var position  = VehiclePosition.Create(ValidVehicleId, ValidTenantId, localTime, 40.0, -74.0);
        position.Time.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData(5.0, 1.2, true)]   // good accuracy
    [InlineData(60.0, 1.5, false)] // accuracy too poor
    [InlineData(5.0, 3.0, false)]  // hdop too high
    [InlineData(null, null, true)] // no data = assume good
    public void HasGoodAccuracy_ShouldReturnCorrectResult(double? accuracyM, double? hdop, bool expected)
    {
        var position = VehiclePosition.Create(
            ValidVehicleId, ValidTenantId, ValidTime, 40.0, -74.0,
            accuracyM: accuracyM, hdop: hdop);

        position.HasGoodAccuracy().Should().Be(expected);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrow()
    {
        var act = () => VehiclePosition.Create(ValidVehicleId, Guid.Empty, ValidTime, 40.0, -74.0);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0.0)]     // stopped — valid
    [InlineData(0.001)]   // very slow — valid
    public void Create_WithZeroOrNearlyzeroSpeed_ShouldSucceed(double speed)
    {
        var position = VehiclePosition.Create(
            ValidVehicleId, ValidTenantId, ValidTime, 40.0, -74.0, speedKmh: speed);
        position.SpeedKmh.Should().Be(speed);
    }

    [Theory]
    [InlineData(0.0)]    // empty tank
    [InlineData(100.0)]  // full tank
    public void Create_WithFuelAtBoundary_ShouldSucceed(double fuel)
    {
        var position = VehiclePosition.Create(
            ValidVehicleId, ValidTenantId, ValidTime, 40.0, -74.0, fuelPct: fuel);
        position.FuelPct.Should().Be(fuel);
    }

    [Fact]
    public void Create_WithEmptyObd2Codes_ShouldStoreEmptyArray()
    {
        // Empty array is different from null — device reported no codes explicitly
        var position = VehiclePosition.Create(
            ValidVehicleId, ValidTenantId, ValidTime, 40.0, -74.0, obd2Codes: Array.Empty<string>());
        position.Obd2Codes.Should().NotBeNull().And.BeEmpty();
    }

    [Theory]
    [InlineData(-90.0)]   // south pole — valid
    [InlineData(90.0)]    // north pole — valid
    [InlineData(0.0)]     // equator — valid
    public void Create_WithLatitudeAtBoundaries_ShouldSucceed(double latitude)
    {
        var act = () => VehiclePosition.Create(ValidVehicleId, ValidTenantId, ValidTime, latitude, 0);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-180.0)]  // antimeridian — valid
    [InlineData(180.0)]   // antimeridian — valid
    public void Create_WithLongitudeAtBoundaries_ShouldSucceed(double longitude)
    {
        var act = () => VehiclePosition.Create(ValidVehicleId, ValidTenantId, ValidTime, 0, longitude);
        act.Should().NotThrow();
    }
}
