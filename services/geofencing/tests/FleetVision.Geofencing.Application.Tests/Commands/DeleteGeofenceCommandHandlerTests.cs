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

public sealed class DeleteGeofenceCommandHandlerTests : IDisposable
{
    private readonly GeofencingDbContext _db;
    private readonly GeometryFactory _factory;
    private readonly DeleteGeofenceCommandHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public DeleteGeofenceCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<GeofencingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        _db      = new GeofencingDbContext(options);
        _handler = new DeleteGeofenceCommandHandler(_db);
    }

    private static double[][][] Ring() => new[]
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

    private async Task<Guid> SeedGeofenceAsync(string name = "To Delete")
    {
        var limits = Substitute.For<ITenantLimitsClient>();
        limits.GetLimitsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new TenantLimitsResponse(_tenantId, "Starter", 25, 25, 50, true));

        var createHandler = new CreateGeofenceCommandHandler(_db, limits, _factory);
        var created = await createHandler.Handle(
            new CreateGeofenceCommand(_tenantId, name, Ring()), default);
        return created.Id;
    }

    [Fact]
    public async Task Handle_WithExistingGeofence_ShouldRemoveIt()
    {
        var id = await SeedGeofenceAsync();

        await _handler.Handle(new DeleteGeofenceCommand(id, _tenantId), default);

        var exists = await _db.Geofences.AnyAsync(g => g.Id == id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenGeofenceNotFound_ShouldThrowGeofenceNotFoundException()
    {
        var act = async () => await _handler.Handle(
            new DeleteGeofenceCommand(Guid.NewGuid(), _tenantId), default);

        await act.Should().ThrowAsync<GeofenceNotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenGeofenceBelongsToAnotherTenant_ShouldThrowAndNotDelete()
    {
        var id = await SeedGeofenceAsync();
        var otherTenant = Guid.NewGuid();

        var act = async () => await _handler.Handle(
            new DeleteGeofenceCommand(id, otherTenant), default);

        await act.Should().ThrowAsync<GeofenceNotFoundException>();
        (await _db.Geofences.AnyAsync(g => g.Id == id)).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldOnlyDeleteTargetGeofence()
    {
        var idA = await SeedGeofenceAsync("Zone A");
        var idB = await SeedGeofenceAsync("Zone B");

        await _handler.Handle(new DeleteGeofenceCommand(idA, _tenantId), default);

        (await _db.Geofences.AnyAsync(g => g.Id == idA)).Should().BeFalse();
        (await _db.Geofences.AnyAsync(g => g.Id == idB)).Should().BeTrue();
    }

    public void Dispose() => _db.Dispose();
}

public sealed class DeleteGeofenceCommandValidatorTests
{
    private readonly DeleteGeofenceCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_ShouldPass()
    {
        _validator.Validate(new DeleteGeofenceCommand(Guid.NewGuid(), Guid.NewGuid()))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyId_ShouldFail()
    {
        _validator.Validate(new DeleteGeofenceCommand(Guid.Empty, Guid.NewGuid()))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithEmptyTenantId_ShouldFail()
    {
        _validator.Validate(new DeleteGeofenceCommand(Guid.NewGuid(), Guid.Empty))
            .IsValid.Should().BeFalse();
    }
}
