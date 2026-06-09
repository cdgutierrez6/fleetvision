using FleetVision.Geofencing.Application.Common;
using FleetVision.Geofencing.Application.Geofences.Commands;
using FleetVision.Geofencing.Domain.Exceptions;
using FleetVision.Geofencing.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NSubstitute;
using Xunit;

namespace FleetVision.Geofencing.Application.Tests.Commands;

public sealed class CreateGeofenceCommandHandlerTests : IDisposable
{
    private readonly GeofencingDbContext _db;
    private readonly GeometryFactory _factory;
    private readonly Guid _tenantId = Guid.NewGuid();

    public CreateGeofenceCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<GeofencingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        _db      = new GeofencingDbContext(options);
    }

    private static double[][][] ManhattanRing() => new[]
    {
        new double[][]
        {
            new[] { -74.010, 40.710 },
            new[] { -74.000, 40.710 },
            new[] { -74.000, 40.720 },
            new[] { -74.010, 40.720 },
            new[] { -74.010, 40.710 }
        }
    };

    private ITenantLimitsClient MockLimits(int maxGeofences)
    {
        var client = Substitute.For<ITenantLimitsClient>();
        client.GetLimitsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new TenantLimitsResponse(_tenantId, "Free", 25, 10, maxGeofences, true));
        return client;
    }

    // ─── GAP-5: Plan limit enforcement ────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenAtPlanLimit_ShouldThrowGeofencePlanLimitExceededException()
    {
        var limitsClient = MockLimits(maxGeofences: 1);
        var handler = new CreateGeofenceCommandHandler(_db, limitsClient, _factory);

        // Create the one allowed geofence
        await handler.Handle(
            new CreateGeofenceCommand(_tenantId, "Zone A", ManhattanRing()), default);

        // Second creation should be blocked
        var act = async () => await handler.Handle(
            new CreateGeofenceCommand(_tenantId, "Zone B", ManhattanRing()), default);

        await act.Should().ThrowAsync<GeofencePlanLimitExceededException>();
    }

    [Fact]
    public async Task Handle_WhenBelowPlanLimit_ShouldCreateSuccessfully()
    {
        var limitsClient = MockLimits(maxGeofences: 10);
        var handler = new CreateGeofenceCommandHandler(_db, limitsClient, _factory);

        var result = await handler.Handle(
            new CreateGeofenceCommand(_tenantId, "Zone A", ManhattanRing()), default);

        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("Zone A");
    }

    [Fact]
    public async Task Handle_DuplicateName_ShouldThrowGeofenceNameAlreadyExistsException()
    {
        var limitsClient = MockLimits(maxGeofences: 10);
        var handler = new CreateGeofenceCommandHandler(_db, limitsClient, _factory);

        await handler.Handle(
            new CreateGeofenceCommand(_tenantId, "Duplicate Zone", ManhattanRing()), default);

        var act = async () => await handler.Handle(
            new CreateGeofenceCommand(_tenantId, "Duplicate Zone", ManhattanRing()), default);

        await act.Should().ThrowAsync<GeofenceNameAlreadyExistsException>();
    }

    [Fact]
    public async Task Handle_SameNameDifferentTenant_ShouldCreateSuccessfully()
    {
        var tenant2 = Guid.NewGuid();

        var limits1 = Substitute.For<ITenantLimitsClient>();
        limits1.GetLimitsAsync(_tenantId, Arg.Any<CancellationToken>())
            .Returns(new TenantLimitsResponse(_tenantId, "Starter", 25, 10, 10, true));
        limits1.GetLimitsAsync(tenant2, Arg.Any<CancellationToken>())
            .Returns(new TenantLimitsResponse(tenant2, "Starter", 25, 10, 10, true));

        var handler = new CreateGeofenceCommandHandler(_db, limits1, _factory);

        await handler.Handle(
            new CreateGeofenceCommand(_tenantId, "Common Zone", ManhattanRing()), default);

        var result = await handler.Handle(
            new CreateGeofenceCommand(tenant2, "Common Zone", ManhattanRing()), default);

        result.Id.Should().NotBeEmpty();
    }

    public void Dispose() => _db.Dispose();
}
