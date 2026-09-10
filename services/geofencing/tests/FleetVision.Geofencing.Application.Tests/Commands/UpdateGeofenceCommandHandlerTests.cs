using FleetVision.Geofencing.Application.Common;
using FleetVision.Geofencing.Application.Geofences.Commands;
using FleetVision.Geofencing.Domain.Enums;
using FleetVision.Geofencing.Domain.Exceptions;
using FleetVision.Geofencing.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NSubstitute;
using Xunit;

namespace FleetVision.Geofencing.Application.Tests.Commands;

public sealed class UpdateGeofenceCommandHandlerTests : IDisposable
{
    private readonly GeofencingDbContext _db;
    private readonly GeometryFactory _factory;
    private readonly UpdateGeofenceCommandHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public UpdateGeofenceCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<GeofencingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        _db      = new GeofencingDbContext(options);
        _handler = new UpdateGeofenceCommandHandler(_db, _factory);
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

    private static double[][][] OtherRing() => new[]
    {
        new double[][]
        {
            new[] { -75.010, 41.710 },
            new[] { -75.000, 41.710 },
            new[] { -75.000, 41.720 },
            new[] { -75.010, 41.720 },
            new[] { -75.010, 41.710 }
        }
    };

    private async Task<Guid> SeedGeofenceAsync(string name)
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
    public async Task Handle_WithValidData_ShouldUpdateGeofence()
    {
        var id = await SeedGeofenceAsync("Original");

        var result = await _handler.Handle(new UpdateGeofenceCommand(
            id, _tenantId, "Renamed", OtherRing(), "New description",
            80, "08:00", "18:00", GeofenceDirection.EntryOnly), default);

        result.Name.Should().Be("Renamed");
        result.Description.Should().Be("New description");
        result.MaxSpeedKmh.Should().Be(80);
        result.AllowedFrom.Should().Be("08:00");
        result.AllowedTo.Should().Be("18:00");
        result.Direction.Should().Be("EntryOnly");
    }

    [Fact]
    public async Task Handle_ShouldPersistChanges()
    {
        var id = await SeedGeofenceAsync("Original");

        await _handler.Handle(new UpdateGeofenceCommand(
            id, _tenantId, "Persisted", Ring(), null, null, null, null,
            GeofenceDirection.Both), default);

        var stored = await _db.Geofences.AsNoTracking().FirstAsync(g => g.Id == id);
        stored.Name.Should().Be("Persisted");
    }

    [Fact]
    public async Task Handle_WhenGeofenceNotFound_ShouldThrowGeofenceNotFoundException()
    {
        var act = async () => await _handler.Handle(new UpdateGeofenceCommand(
            Guid.NewGuid(), _tenantId, "Ghost", Ring(), null, null, null, null,
            GeofenceDirection.Both), default);

        await act.Should().ThrowAsync<GeofenceNotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenGeofenceBelongsToAnotherTenant_ShouldThrowGeofenceNotFoundException()
    {
        var id = await SeedGeofenceAsync("Tenant A zone");
        var otherTenant = Guid.NewGuid();

        var act = async () => await _handler.Handle(new UpdateGeofenceCommand(
            id, otherTenant, "Hijack", Ring(), null, null, null, null,
            GeofenceDirection.Both), default);

        await act.Should().ThrowAsync<GeofenceNotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenRenamingToExistingName_ShouldThrowGeofenceNameAlreadyExistsException()
    {
        await SeedGeofenceAsync("Zone A");
        var idB = await SeedGeofenceAsync("Zone B");

        var act = async () => await _handler.Handle(new UpdateGeofenceCommand(
            idB, _tenantId, "Zone A", Ring(), null, null, null, null,
            GeofenceDirection.Both), default);

        await act.Should().ThrowAsync<GeofenceNameAlreadyExistsException>();
    }

    [Fact]
    public async Task Handle_WhenKeepingSameName_ShouldNotThrowNameConflict()
    {
        var id = await SeedGeofenceAsync("Stable");

        var result = await _handler.Handle(new UpdateGeofenceCommand(
            id, _tenantId, "Stable", OtherRing(), "changed desc", null, null, null,
            GeofenceDirection.Both), default);

        result.Description.Should().Be("changed desc");
    }

    public void Dispose() => _db.Dispose();
}

public sealed class UpdateGeofenceCommandValidatorTests
{
    private readonly UpdateGeofenceCommandValidator _validator = new();

    private static double[][][] ValidRing() => new[]
    {
        new double[][]
        {
            new[] { -74.010, 40.710 },
            new[] { -74.000, 40.710 },
            new[] { -74.000, 40.720 },
            new[] { -74.010, 40.710 }
        }
    };

    private static UpdateGeofenceCommand Valid() => new(
        Guid.NewGuid(), Guid.NewGuid(), "Valid", ValidRing(), null, null, null, null,
        GeofenceDirection.Both);

    [Fact]
    public void Validate_WithValidCommand_ShouldPass()
    {
        _validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyId_ShouldFail()
    {
        var result = _validator.Validate(Valid() with { Id = Guid.Empty });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateGeofenceCommand.Id));
    }

    [Fact]
    public void Validate_WithEmptyTenantId_ShouldFail()
    {
        _validator.Validate(Valid() with { TenantId = Guid.Empty }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithEmptyName_ShouldFail()
    {
        _validator.Validate(Valid() with { Name = "" }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithNameTooLong_ShouldFail()
    {
        _validator.Validate(Valid() with { Name = new string('x', 101) }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithTooFewCoordinates_ShouldFail()
    {
        var badRing = new[] { new double[][] { new[] { 0.0, 0.0 }, new[] { 1.0, 0.0 } } };
        _validator.Validate(Valid() with { Coordinates = badRing }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithNonPositiveMaxSpeed_ShouldFail()
    {
        _validator.Validate(Valid() with { MaxSpeedKmh = 0 }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithPositiveMaxSpeed_ShouldPass()
    {
        _validator.Validate(Valid() with { MaxSpeedKmh = 60 }).IsValid.Should().BeTrue();
    }
}
