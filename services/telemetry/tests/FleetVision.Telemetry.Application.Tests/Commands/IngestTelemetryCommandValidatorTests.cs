using FleetVision.Telemetry.Application.Commands;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace FleetVision.Telemetry.Application.Tests.Commands;

public sealed class IngestTelemetryCommandValidatorTests
{
    private readonly IngestTelemetryCommandValidator _validator = new();

    private static IngestTelemetryCommand ValidCommand() => new(
        VehicleId:       Guid.NewGuid(),
        TenantId:        Guid.NewGuid(),
        DriverId:        null,
        TimestampUnixMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        Latitude:        40.715,
        Longitude:       -74.005,
        SpeedKmh:        null,
        HeadingDeg:      null,
        FuelPct:         null,
        EngineOn:        null,
        Obd2Codes:       null);

    [Fact]
    public void Validate_ValidCommand_ShouldPassWithoutErrors()
    {
        var result = _validator.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyVehicleId_ShouldFail()
    {
        var cmd = ValidCommand() with { VehicleId = Guid.Empty };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.VehicleId);
    }

    [Fact]
    public void Validate_EmptyTenantId_ShouldFail()
    {
        var cmd = ValidCommand() with { TenantId = Guid.Empty };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000000)]
    public void Validate_NonPositiveTimestamp_ShouldFail(long ts)
    {
        var cmd = ValidCommand() with { TimestampUnixMs = ts };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.TimestampUnixMs);
    }

    [Theory]
    [InlineData(-90.0001)]
    [InlineData(90.0001)]
    [InlineData(200.0)]
    [InlineData(-200.0)]
    public void Validate_LatitudeOutOfRange_ShouldFail(double latitude)
    {
        var cmd = ValidCommand() with { Latitude = latitude };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Latitude);
    }

    [Theory]
    [InlineData(-90.0)]  // boundary — valid
    [InlineData(90.0)]   // boundary — valid
    [InlineData(0.0)]
    public void Validate_LatitudeAtBoundary_ShouldPass(double latitude)
    {
        var cmd = ValidCommand() with { Latitude = latitude };
        _validator.TestValidate(cmd).ShouldNotHaveValidationErrorFor(x => x.Latitude);
    }

    [Theory]
    [InlineData(-180.0001)]
    [InlineData(180.0001)]
    public void Validate_LongitudeOutOfRange_ShouldFail(double longitude)
    {
        var cmd = ValidCommand() with { Longitude = longitude };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Longitude);
    }

    [Theory]
    [InlineData(-180.0)]  // boundary — valid
    [InlineData(180.0)]   // boundary — valid
    public void Validate_LongitudeAtBoundary_ShouldPass(double longitude)
    {
        var cmd = ValidCommand() with { Longitude = longitude };
        _validator.TestValidate(cmd).ShouldNotHaveValidationErrorFor(x => x.Longitude);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(-100.0f)]
    public void Validate_NegativeSpeed_ShouldFail(float speed)
    {
        var cmd = ValidCommand() with { SpeedKmh = speed };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.SpeedKmh);
    }

    [Theory]
    [InlineData(0.0f)]    // exactly 0 — valid (vehicle stopped)
    [InlineData(120.0f)]  // normal highway speed
    public void Validate_ValidSpeed_ShouldPass(float speed)
    {
        var cmd = ValidCommand() with { SpeedKmh = speed };
        _validator.TestValidate(cmd).ShouldNotHaveValidationErrorFor(x => x.SpeedKmh);
    }

    [Fact]
    public void Validate_NullSpeed_ShouldPass()
    {
        var cmd = ValidCommand() with { SpeedKmh = null };
        _validator.TestValidate(cmd).ShouldNotHaveValidationErrorFor(x => x.SpeedKmh);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(100.1f)]
    [InlineData(200.0f)]
    public void Validate_FuelOutOfRange_ShouldFail(float fuel)
    {
        var cmd = ValidCommand() with { FuelPct = fuel };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.FuelPct);
    }

    [Theory]
    [InlineData(0.0f)]    // empty tank — valid
    [InlineData(50.0f)]
    [InlineData(100.0f)]  // full tank — valid
    public void Validate_FuelAtBoundary_ShouldPass(float fuel)
    {
        var cmd = ValidCommand() with { FuelPct = fuel };
        _validator.TestValidate(cmd).ShouldNotHaveValidationErrorFor(x => x.FuelPct);
    }

    [Fact]
    public void Validate_MultipleErrors_ShouldReturnAll()
    {
        var cmd = ValidCommand() with
        {
            VehicleId      = Guid.Empty,
            Latitude       = 999.0,
            TimestampUnixMs = 0,
        };

        var result = _validator.TestValidate(cmd);

        result.Errors.Should().HaveCountGreaterThanOrEqualTo(3);
        result.ShouldHaveValidationErrorFor(x => x.VehicleId);
        result.ShouldHaveValidationErrorFor(x => x.Latitude);
        result.ShouldHaveValidationErrorFor(x => x.TimestampUnixMs);
    }
}
