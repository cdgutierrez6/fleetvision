using FleetVision.Geofencing.Domain.Entities;
using FleetVision.Geofencing.Domain.Enums;
using FleetVision.Geofencing.Domain.Exceptions;
using FluentAssertions;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Xunit;

namespace FleetVision.Geofencing.Domain.Tests.Entities;

public sealed class GeofenceTests
{
    private static readonly GeometryFactory Factory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    private static Polygon ValidSquare() => Factory.CreatePolygon(new[]
    {
        new Coordinate(-74.010, 40.710),
        new Coordinate(-74.000, 40.710),
        new Coordinate(-74.000, 40.720),
        new Coordinate(-74.010, 40.720),
        new Coordinate(-74.010, 40.710)  // closing point
    });

    [Fact]
    public void Create_WithValidData_ShouldCreateActiveGeofence()
    {
        var tenantId = Guid.NewGuid();
        var polygon  = ValidSquare();

        var geofence = Geofence.Create(tenantId, "Manhattan Zone", polygon, "Downtown zone");

        geofence.Id.Should().NotBeEmpty();
        geofence.TenantId.Should().Be(tenantId);
        geofence.Name.Should().Be("Manhattan Zone");
        geofence.IsActive.Should().BeTrue();
        geofence.Direction.Should().Be(GeofenceDirection.Both);
    }

    [Fact]
    public void Create_WithSpeedLimit_ShouldSetMaxSpeed()
    {
        var geofence = Geofence.Create(Guid.NewGuid(), "Speed Zone", ValidSquare(), maxSpeedKmh: 50);

        geofence.MaxSpeedKmh.Should().Be(50);
    }

    [Fact]
    public void Create_WithTimeWindow_ShouldSetAllowedHours()
    {
        var from = new TimeOnly(8, 0);
        var to   = new TimeOnly(18, 0);

        var geofence = Geofence.Create(Guid.NewGuid(), "Zone", ValidSquare(),
            allowedFrom: from, allowedTo: to);

        geofence.AllowedFrom.Should().Be(from);
        geofence.AllowedTo.Should().Be(to);
    }

    [Fact]
    public void Create_WithOnlyAllowedFrom_ShouldThrow()
    {
        var act = () => Geofence.Create(Guid.NewGuid(), "Zone", ValidSquare(),
            allowedFrom: new TimeOnly(8, 0), allowedTo: null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithPolygonLessThan3Vertices_ShouldThrow()
    {
        var invalidPolygon = Factory.CreatePolygon(new[]
        {
            new Coordinate(0, 0),
            new Coordinate(1, 0),
            new Coordinate(0, 0) // only 2 distinct
        });

        var act = () => Geofence.Create(Guid.NewGuid(), "Zone", invalidPolygon);
        act.Should().Throw<InvalidPolygonException>();
    }

    [Fact]
    public void Create_WithWrongSrid_ShouldThrow()
    {
        var factory3857 = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 3857);
        var polygon     = factory3857.CreatePolygon(new[]
        {
            new Coordinate(0, 0), new Coordinate(1, 0),
            new Coordinate(1, 1), new Coordinate(0, 0)
        });

        var act = () => Geofence.Create(Guid.NewGuid(), "Zone", polygon);
        act.Should().Throw<InvalidPolygonException>().WithMessage("*4326*");
    }

    [Fact]
    public void Create_WithNegativeSpeedLimit_ShouldThrow()
    {
        var act = () => Geofence.Create(Guid.NewGuid(), "Zone", ValidSquare(), maxSpeedKmh: -10);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ContainsPoint_WhenPointIsInside_ShouldReturnTrue()
    {
        var geofence     = Geofence.Create(Guid.NewGuid(), "Zone", ValidSquare());
        var insidePoint  = Factory.CreatePoint(new Coordinate(-74.005, 40.715));

        geofence.ContainsPoint(insidePoint).Should().BeTrue();
    }

    [Fact]
    public void ContainsPoint_WhenPointIsOutside_ShouldReturnFalse()
    {
        var geofence      = Geofence.Create(Guid.NewGuid(), "Zone", ValidSquare());
        var outsidePoint  = Factory.CreatePoint(new Coordinate(-75.000, 41.000));

        geofence.ContainsPoint(outsidePoint).Should().BeFalse();
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var geofence = Geofence.Create(Guid.NewGuid(), "Zone", ValidSquare());
        geofence.Deactivate();
        geofence.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_AfterDeactivate_ShouldRestoreIsActive()
    {
        var geofence = Geofence.Create(Guid.NewGuid(), "Zone", ValidSquare());
        geofence.Deactivate();
        geofence.Activate();
        geofence.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Update_ShouldChangeName()
    {
        var geofence = Geofence.Create(Guid.NewGuid(), "Old Name", ValidSquare());
        geofence.Update("New Name", ValidSquare(), null, null, null, null, GeofenceDirection.EntryOnly);
        geofence.Name.Should().Be("New Name");
        geofence.Direction.Should().Be(GeofenceDirection.EntryOnly);
    }

    [Fact]
    public void ContainsPoint_WhenPointIsExactlyOnBoundary_ShouldReturnFalse()
    {
        // NTS Polygon.Contains() uses DE-9IM: boundary points are NOT "inside" the polygon.
        // This matches real geofencing semantics (crossing the line = transition, not arrival).
        var geofence      = Geofence.Create(Guid.NewGuid(), "Zone", ValidSquare());
        var boundaryPoint = Factory.CreatePoint(new Coordinate(-74.010, 40.715)); // on the west edge

        geofence.ContainsPoint(boundaryPoint).Should().BeFalse();
    }
}
