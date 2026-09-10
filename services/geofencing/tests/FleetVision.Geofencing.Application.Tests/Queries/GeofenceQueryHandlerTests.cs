using FleetVision.Geofencing.Application.Common;
using FleetVision.Geofencing.Application.Geofences.Commands;
using FleetVision.Geofencing.Application.Geofences.Queries;
using FleetVision.Geofencing.Domain.Exceptions;
using FleetVision.Geofencing.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NSubstitute;
using Xunit;

namespace FleetVision.Geofencing.Application.Tests.Queries;

public sealed class GeofenceQueryHandlerTests : IDisposable
{
    private readonly GeofencingDbContext _db;
    private readonly GeometryFactory _factory;
    private readonly Guid _tenantId = Guid.NewGuid();

    public GeofenceQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<GeofencingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        _db      = new GeofencingDbContext(options);
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

    private async Task<Guid> SeedGeofenceAsync(string name, Guid? tenantId = null)
    {
        var tid = tenantId ?? _tenantId;
        var limits = Substitute.For<ITenantLimitsClient>();
        limits.GetLimitsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new TenantLimitsResponse(tid, "Starter", 25, 25, 50, true));

        var createHandler = new CreateGeofenceCommandHandler(_db, limits, _factory);
        var created = await createHandler.Handle(
            new CreateGeofenceCommand(tid, name, Ring()), default);
        return created.Id;
    }

    // ─── GetGeofenceQueryHandler ────────────────────────────────────────────

    [Fact]
    public async Task Get_WithExistingGeofence_ShouldReturnDto()
    {
        var id = await SeedGeofenceAsync("Target");
        var handler = new GetGeofenceQueryHandler(_db);

        var result = await handler.Handle(new GetGeofenceQuery(id, _tenantId), default);

        result.Id.Should().Be(id);
        result.Name.Should().Be("Target");
        result.Boundary.Type.Should().Be("Polygon");
    }

    [Fact]
    public async Task Get_WhenNotFound_ShouldThrowGeofenceNotFoundException()
    {
        var handler = new GetGeofenceQueryHandler(_db);

        var act = async () => await handler.Handle(
            new GetGeofenceQuery(Guid.NewGuid(), _tenantId), default);

        await act.Should().ThrowAsync<GeofenceNotFoundException>();
    }

    [Fact]
    public async Task Get_WhenBelongsToAnotherTenant_ShouldThrowGeofenceNotFoundException()
    {
        var id = await SeedGeofenceAsync("Tenant A only");
        var handler = new GetGeofenceQueryHandler(_db);

        var act = async () => await handler.Handle(
            new GetGeofenceQuery(id, Guid.NewGuid()), default);

        await act.Should().ThrowAsync<GeofenceNotFoundException>();
    }

    // ─── ListGeofencesQueryHandler ──────────────────────────────────────────

    [Fact]
    public async Task List_ShouldReturnOnlyCurrentTenantGeofences()
    {
        await SeedGeofenceAsync("Mine 1");
        await SeedGeofenceAsync("Mine 2");
        await SeedGeofenceAsync("Foreign", tenantId: Guid.NewGuid());

        var handler = new ListGeofencesQueryHandler(_db);
        var result = await handler.Handle(new ListGeofencesQuery(_tenantId, 1, 10), default);

        result.Total.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(g => g.TenantId == _tenantId);
    }

    [Fact]
    public async Task List_ShouldPaginate()
    {
        for (var i = 0; i < 5; i++)
            await SeedGeofenceAsync($"Zone {i}");

        var handler = new ListGeofencesQueryHandler(_db);
        var page1 = await handler.Handle(new ListGeofencesQuery(_tenantId, 1, 2), default);
        var page2 = await handler.Handle(new ListGeofencesQuery(_tenantId, 2, 2), default);
        var page3 = await handler.Handle(new ListGeofencesQuery(_tenantId, 3, 2), default);

        page1.Total.Should().Be(5);
        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(2);
        page3.Items.Should().HaveCount(1);

        page1.Items.Select(g => g.Id).Should().NotIntersectWith(page2.Items.Select(g => g.Id));
    }

    [Fact]
    public async Task List_WithNoGeofences_ShouldReturnEmpty()
    {
        var handler = new ListGeofencesQueryHandler(_db);
        var result = await handler.Handle(new ListGeofencesQuery(_tenantId, 1, 10), default);

        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task List_ShouldOrderByCreatedAtAscending()
    {
        var first  = await SeedGeofenceAsync("First");
        var second = await SeedGeofenceAsync("Second");

        var handler = new ListGeofencesQueryHandler(_db);
        var result = await handler.Handle(new ListGeofencesQuery(_tenantId, 1, 10), default);

        result.Items[0].Id.Should().Be(first);
        result.Items[1].Id.Should().Be(second);
    }

    public void Dispose() => _db.Dispose();
}
