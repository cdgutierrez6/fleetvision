using FleetVision.Geofencing.Application.Common;
using FleetVision.Geofencing.Application.Geofences.Commands;
using FleetVision.Geofencing.Application.TelemetryEvaluation;
using FleetVision.Geofencing.Domain.Enums;
using FleetVision.Geofencing.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NSubstitute;
using Xunit;

namespace FleetVision.Geofencing.Application.Tests.Commands;

public sealed class EvaluateTelemetryEventHandlerTests : IDisposable
{
    private readonly GeofencingDbContext _db;
    private readonly EvaluateTelemetryEventCommandHandler _handler;
    private readonly GeometryFactory _factory;
    private readonly IViolationPublisher _publisher;
    private readonly Guid _tenantId = Guid.NewGuid();

    public EvaluateTelemetryEventHandlerTests()
    {
        var options = new DbContextOptionsBuilder<GeofencingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _factory   = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        _db        = new GeofencingDbContext(options);
        _publisher = Substitute.For<IViolationPublisher>();
        _handler   = new EvaluateTelemetryEventCommandHandler(_db, _factory, _publisher);
    }

    private async Task<Guid> SeedGeofenceAsync(
        int? maxSpeedKmh = null,
        TimeOnly? allowedFrom = null,
        TimeOnly? allowedTo = null,
        GeofenceDirection direction = GeofenceDirection.Both,
        string name = "Test Zone",
        double[][]? ring = null)
    {
        var limitsClient = Substitute.For<ITenantLimitsClient>();
        limitsClient.GetLimitsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new TenantLimitsResponse(_tenantId, "Starter", 25, 25, 50, true));

        // Default: Manhattan square -74.010…-74.000 lon, 40.710…40.720 lat
        ring ??= new double[][]
        {
            new[] { -74.010, 40.710 },
            new[] { -74.000, 40.710 },
            new[] { -74.000, 40.720 },
            new[] { -74.010, 40.720 },
            new[] { -74.010, 40.710 }
        };

        var coords = new double[][][] { ring };

        var handler = new CreateGeofenceCommandHandler(_db, limitsClient, _factory);
        var result  = await handler.Handle(
            new CreateGeofenceCommand(_tenantId, name, coords, null, maxSpeedKmh,
                allowedFrom?.ToString("HH:mm"), allowedTo?.ToString("HH:mm"), direction), default);

        return result.Id;
    }

    [Fact]
    public async Task Handle_VehicleEnteringZone_ShouldGenerateZoneEnteredViolation()
    {
        await SeedGeofenceAsync();

        // Vehicle outside initially, now inside
        var result = await _handler.Handle(new EvaluateTelemetryEventCommand(
            _tenantId, Guid.NewGuid(), null,
            -74.005, 40.715,  // inside the square
            30, DateTime.UtcNow), default);

        result.ViolationsDetected.Should().Be(1);
        result.Violations[0].ViolationType.Should().Be("ZoneEntered");
    }

    [Fact]
    public async Task Handle_VehicleExitingZone_ShouldGenerateZoneExitedViolation()
    {
        await SeedGeofenceAsync();
        var vehicleId = Guid.NewGuid();

        // First ping: vehicle enters
        await _handler.Handle(new EvaluateTelemetryEventCommand(
            _tenantId, vehicleId, null, -74.005, 40.715, 30, DateTime.UtcNow), default);

        // Second ping: vehicle exits
        var result = await _handler.Handle(new EvaluateTelemetryEventCommand(
            _tenantId, vehicleId, null, -75.000, 41.000, 30, DateTime.UtcNow), default);

        result.ViolationsDetected.Should().Be(1);
        result.Violations[0].ViolationType.Should().Be("ZoneExited");
    }

    [Fact]
    public async Task Handle_VehicleSpeedExceedsLimit_ShouldGenerateSpeedViolation()
    {
        await SeedGeofenceAsync(maxSpeedKmh: 50);
        var vehicleId = Guid.NewGuid();

        // Vehicle enters at speed 80 > 50 → ZoneEntered + SpeedExceeded
        var result = await _handler.Handle(new EvaluateTelemetryEventCommand(
            _tenantId, vehicleId, null, -74.005, 40.715, 80, DateTime.UtcNow), default);

        result.ViolationsDetected.Should().Be(2);
        result.Violations.Should().Contain(v => v.ViolationType == "SpeedExceeded");
        result.Violations.Should().Contain(v => v.ViolationType == "ZoneEntered");
    }

    [Fact]
    public async Task Handle_VehicleWithinSpeedLimit_ShouldNotGenerateSpeedViolation()
    {
        await SeedGeofenceAsync(maxSpeedKmh: 50);
        var vehicleId = Guid.NewGuid();

        var result = await _handler.Handle(new EvaluateTelemetryEventCommand(
            _tenantId, vehicleId, null, -74.005, 40.715, 40, DateTime.UtcNow), default);

        result.Violations.Should().NotContain(v => v.ViolationType == "SpeedExceeded");
    }

    [Fact]
    public async Task Handle_VehicleOutOfSchedule_ShouldGenerateOutOfScheduleViolation()
    {
        // Allowed 08:00-18:00, event at 22:00
        await SeedGeofenceAsync(allowedFrom: new TimeOnly(8, 0), allowedTo: new TimeOnly(18, 0));
        var vehicleId = Guid.NewGuid();
        var lateTime  = DateTime.UtcNow.Date.Add(new TimeSpan(22, 0, 0));

        var result = await _handler.Handle(new EvaluateTelemetryEventCommand(
            _tenantId, vehicleId, null, -74.005, 40.715, 30, lateTime), default);

        result.Violations.Should().Contain(v => v.ViolationType == "OutOfSchedule");
    }

    [Fact]
    public async Task Handle_VehicleInSchedule_ShouldNotGenerateOutOfScheduleViolation()
    {
        await SeedGeofenceAsync(allowedFrom: new TimeOnly(8, 0), allowedTo: new TimeOnly(18, 0));
        var vehicleId  = Guid.NewGuid();
        var noonTime   = DateTime.UtcNow.Date.Add(new TimeSpan(12, 0, 0));

        var result = await _handler.Handle(new EvaluateTelemetryEventCommand(
            _tenantId, vehicleId, null, -74.005, 40.715, 30, noonTime), default);

        result.Violations.Should().NotContain(v => v.ViolationType == "OutOfSchedule");
    }

    [Fact]
    public async Task Handle_ExitOnlyGeofence_ShouldNotGenerateZoneEnteredViolation()
    {
        await SeedGeofenceAsync(direction: GeofenceDirection.ExitOnly);
        var vehicleId = Guid.NewGuid();

        var result = await _handler.Handle(new EvaluateTelemetryEventCommand(
            _tenantId, vehicleId, null, -74.005, 40.715, 30, DateTime.UtcNow), default);

        result.Violations.Should().NotContain(v => v.ViolationType == "ZoneEntered");
    }

    [Fact]
    public async Task Handle_VehicleStaysOutside_ShouldGenerateNoViolations()
    {
        await SeedGeofenceAsync();

        var result = await _handler.Handle(new EvaluateTelemetryEventCommand(
            _tenantId, Guid.NewGuid(), null, -75.000, 41.000, 30, DateTime.UtcNow), default);

        result.ViolationsDetected.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldPersistVehicleGeofenceState()
    {
        await SeedGeofenceAsync();
        var vehicleId = Guid.NewGuid();

        await _handler.Handle(new EvaluateTelemetryEventCommand(
            _tenantId, vehicleId, null, -74.005, 40.715, 30, DateTime.UtcNow), default);

        var state = await _db.VehicleGeofenceStates
            .FirstOrDefaultAsync(s => s.VehicleId == vehicleId);

        state.Should().NotBeNull();
        state!.IsInside.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoActiveGeofences_ShouldReturnZeroViolations()
    {
        // No geofences seeded
        var result = await _handler.Handle(new EvaluateTelemetryEventCommand(
            _tenantId, Guid.NewGuid(), null, -74.005, 40.715, 30, DateTime.UtcNow), default);

        result.ViolationsDetected.Should().Be(0);
    }

    // ─── GAP-2: IsInSchedule overnight window ────────────────────────────────

    [Fact]
    public async Task Handle_OvernightSchedule_VehicleAtNight_ShouldBeInSchedule()
    {
        // AllowedFrom=22:00, AllowedTo=06:00 — event at 23:30 → inside window
        await SeedGeofenceAsync(allowedFrom: new TimeOnly(22, 0), allowedTo: new TimeOnly(6, 0));
        var vehicleId  = Guid.NewGuid();
        var nightTime  = DateTime.UtcNow.Date.Add(new TimeSpan(23, 30, 0));

        var result = await _handler.Handle(new EvaluateTelemetryEventCommand(
            _tenantId, vehicleId, null, -74.005, 40.715, 30, nightTime), default);

        result.Violations.Should().NotContain(v => v.ViolationType == "OutOfSchedule");
    }

    [Fact]
    public async Task Handle_OvernightSchedule_VehicleAtEarlyMorning_ShouldBeInSchedule()
    {
        // AllowedFrom=22:00, AllowedTo=06:00 — event at 05:00 → inside overnight window
        await SeedGeofenceAsync(allowedFrom: new TimeOnly(22, 0), allowedTo: new TimeOnly(6, 0));
        var vehicleId   = Guid.NewGuid();
        var earlyTime   = DateTime.UtcNow.Date.Add(new TimeSpan(5, 0, 0));

        var result = await _handler.Handle(new EvaluateTelemetryEventCommand(
            _tenantId, vehicleId, null, -74.005, 40.715, 30, earlyTime), default);

        result.Violations.Should().NotContain(v => v.ViolationType == "OutOfSchedule");
    }

    [Fact]
    public async Task Handle_OvernightSchedule_VehicleAtNoon_ShouldGenerateOutOfScheduleViolation()
    {
        // AllowedFrom=22:00, AllowedTo=06:00 — event at 12:00 → outside overnight window
        await SeedGeofenceAsync(allowedFrom: new TimeOnly(22, 0), allowedTo: new TimeOnly(6, 0));
        var vehicleId = Guid.NewGuid();
        var noonTime  = DateTime.UtcNow.Date.Add(new TimeSpan(12, 0, 0));

        var result = await _handler.Handle(new EvaluateTelemetryEventCommand(
            _tenantId, vehicleId, null, -74.005, 40.715, 30, noonTime), default);

        result.Violations.Should().Contain(v => v.ViolationType == "OutOfSchedule");
    }

    // ─── GAP-3: Multiple simultaneous geofences ──────────────────────────────

    [Fact]
    public async Task Handle_VehicleInsideMultipleGeofences_ShouldGenerateViolationForEach()
    {
        // Geofence A: tight Manhattan square
        await SeedGeofenceAsync(name: "Zone A");

        // Geofence B: larger outer square that also contains -74.005, 40.715
        await SeedGeofenceAsync(name: "Zone B", ring: new double[][]
        {
            new[] { -74.020, 40.700 },
            new[] { -73.990, 40.700 },
            new[] { -73.990, 40.730 },
            new[] { -74.020, 40.730 },
            new[] { -74.020, 40.700 }
        });

        var result = await _handler.Handle(new EvaluateTelemetryEventCommand(
            _tenantId, Guid.NewGuid(), null, -74.005, 40.715, 30, DateTime.UtcNow), default);

        result.ViolationsDetected.Should().Be(2);
        result.Violations.Should().AllSatisfy(v => v.ViolationType.Should().Be("ZoneEntered"));
    }

    // ─── RFC-008: IViolationPublisher integration ───────────────────────────────

    [Fact]
    public async Task Handle_ViolationDetected_ShouldCallPublisherWithCorrectGeofenceName()
    {
        var geofenceId = await SeedGeofenceAsync(name: "Named Zone");
        var vehicleId  = Guid.NewGuid();

        await _handler.Handle(new EvaluateTelemetryEventCommand(
            _tenantId, vehicleId, null, -74.005, 40.715, 30, DateTime.UtcNow), default);

        _publisher.Received(1).Enqueue(
            Arg.Is<FleetVision.Geofencing.Domain.Entities.GeofenceViolation>(v =>
                v.VehicleId == vehicleId),
            "Named Zone");
    }

    [Fact]
    public async Task Handle_NoViolation_ShouldNotCallPublisher()
    {
        await SeedGeofenceAsync();

        // Vehicle is outside the zone
        await _handler.Handle(new EvaluateTelemetryEventCommand(
            _tenantId, Guid.NewGuid(), null, -75.000, 41.000, 30, DateTime.UtcNow), default);

        _publisher.DidNotReceiveWithAnyArgs().Enqueue(default!, default!);
    }

    [Fact]
    public async Task Handle_MultipleViolations_PublisherCalledOncePerViolation()
    {
        // Speed violation (50 kmh limit) — entering also triggers ZoneEntered
        await SeedGeofenceAsync(maxSpeedKmh: 50);

        await _handler.Handle(new EvaluateTelemetryEventCommand(
            _tenantId, Guid.NewGuid(), null, -74.005, 40.715, 80, DateTime.UtcNow), default);

        // ZoneEntered + SpeedExceeded = 2 violations = 2 publisher calls
        _publisher.Received(2).Enqueue(Arg.Any<FleetVision.Geofencing.Domain.Entities.GeofenceViolation>(), Arg.Any<string>());
    }

    public void Dispose() => _db.Dispose();
}
