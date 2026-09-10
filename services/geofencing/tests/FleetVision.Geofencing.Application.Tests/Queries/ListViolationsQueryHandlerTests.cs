using FleetVision.Geofencing.Application.Common;
using FleetVision.Geofencing.Application.Violations.Queries;
using FleetVision.Geofencing.Domain.Entities;
using FleetVision.Geofencing.Domain.Enums;
using FleetVision.Geofencing.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Xunit;

namespace FleetVision.Geofencing.Application.Tests.Queries;

public sealed class ListViolationsQueryHandlerTests : IDisposable
{
    private readonly GeofencingDbContext _db;
    private readonly GeometryFactory _factory;
    private readonly ListViolationsQueryHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _geofenceId = Guid.NewGuid();

    public ListViolationsQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<GeofencingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        _db      = new GeofencingDbContext(options);
        _handler = new ListViolationsQueryHandler(_db);
    }

    private Point Position() => _factory.CreatePoint(new Coordinate(-74.005, 40.715));

    private async Task SeedViolationAsync(
        Guid? tenantId = null,
        Guid? geofenceId = null,
        Guid? vehicleId = null,
        ViolationType type = ViolationType.ZoneEntered,
        double? actualSpeed = null,
        int? limitSpeed = null)
    {
        var violation = GeofenceViolation.Create(
            tenantId ?? _tenantId,
            geofenceId ?? _geofenceId,
            vehicleId ?? Guid.NewGuid(),
            null,
            type,
            Position(),
            actualSpeed,
            limitSpeed);

        _db.Violations.Add(violation);
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_ShouldReturnViolationsForTenantAndGeofence()
    {
        await SeedViolationAsync();
        await SeedViolationAsync();

        var result = await _handler.Handle(
            new ListViolationsQuery(_tenantId, _geofenceId, 1, 10), default);

        result.Total.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(v => v.GeofenceId == _geofenceId);
    }

    [Fact]
    public async Task Handle_ShouldExcludeOtherTenants()
    {
        await SeedViolationAsync();
        await SeedViolationAsync(tenantId: Guid.NewGuid());

        var result = await _handler.Handle(
            new ListViolationsQuery(_tenantId, _geofenceId, 1, 10), default);

        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldExcludeOtherGeofences()
    {
        await SeedViolationAsync();
        await SeedViolationAsync(geofenceId: Guid.NewGuid());

        var result = await _handler.Handle(
            new ListViolationsQuery(_tenantId, _geofenceId, 1, 10), default);

        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithVehicleFilter_ShouldReturnOnlyThatVehicle()
    {
        var vehicle = Guid.NewGuid();
        await SeedViolationAsync(vehicleId: vehicle);
        await SeedViolationAsync(vehicleId: Guid.NewGuid());

        var result = await _handler.Handle(
            new ListViolationsQuery(_tenantId, _geofenceId, 1, 10, VehicleId: vehicle), default);

        result.Total.Should().Be(1);
        result.Items[0].VehicleId.Should().Be(vehicle);
    }

    [Fact]
    public async Task Handle_WithViolationTypeFilter_ShouldReturnOnlyThatType()
    {
        await SeedViolationAsync(type: ViolationType.ZoneEntered);
        await SeedViolationAsync(type: ViolationType.SpeedExceeded, actualSpeed: 90, limitSpeed: 60);

        var result = await _handler.Handle(
            new ListViolationsQuery(_tenantId, _geofenceId, 1, 10,
                ViolationType: ViolationType.SpeedExceeded), default);

        result.Total.Should().Be(1);
        result.Items[0].ViolationType.Should().Be("SpeedExceeded");
        result.Items[0].ActualSpeedKmh.Should().Be(90);
        result.Items[0].LimitSpeedKmh.Should().Be(60);
    }

    [Fact]
    public async Task Handle_WithFromInFuture_ShouldExcludeExistingViolations()
    {
        await SeedViolationAsync();

        var result = await _handler.Handle(
            new ListViolationsQuery(_tenantId, _geofenceId, 1, 10,
                From: DateTime.UtcNow.AddHours(1)), default);

        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithFromInPast_ShouldIncludeExistingViolations()
    {
        await SeedViolationAsync();

        var result = await _handler.Handle(
            new ListViolationsQuery(_tenantId, _geofenceId, 1, 10,
                From: DateTime.UtcNow.AddHours(-1)), default);

        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithToInPast_ShouldExcludeExistingViolations()
    {
        await SeedViolationAsync();

        var result = await _handler.Handle(
            new ListViolationsQuery(_tenantId, _geofenceId, 1, 10,
                To: DateTime.UtcNow.AddHours(-1)), default);

        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithToInFuture_ShouldIncludeExistingViolations()
    {
        await SeedViolationAsync();

        var result = await _handler.Handle(
            new ListViolationsQuery(_tenantId, _geofenceId, 1, 10,
                To: DateTime.UtcNow.AddHours(1)), default);

        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldPaginate()
    {
        for (var i = 0; i < 5; i++)
            await SeedViolationAsync();

        var page1 = await _handler.Handle(
            new ListViolationsQuery(_tenantId, _geofenceId, 1, 2), default);
        var page3 = await _handler.Handle(
            new ListViolationsQuery(_tenantId, _geofenceId, 3, 2), default);

        page1.Total.Should().Be(5);
        page1.Items.Should().HaveCount(2);
        page3.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WithNoViolations_ShouldReturnEmpty()
    {
        var result = await _handler.Handle(
            new ListViolationsQuery(_tenantId, _geofenceId, 1, 10), default);

        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldMapPositionToLatitudeAndLongitude()
    {
        await SeedViolationAsync();

        var result = await _handler.Handle(
            new ListViolationsQuery(_tenantId, _geofenceId, 1, 10), default);

        result.Items[0].Latitude.Should().BeApproximately(40.715, 0.0001);
        result.Items[0].Longitude.Should().BeApproximately(-74.005, 0.0001);
    }

    public void Dispose() => _db.Dispose();
}
