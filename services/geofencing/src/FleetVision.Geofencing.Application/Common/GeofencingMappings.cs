using FleetVision.Geofencing.Application.DTOs;
using FleetVision.Geofencing.Domain.Entities;
using NetTopologySuite.Geometries;

namespace FleetVision.Geofencing.Application.Common;

internal static class GeofencingMappings
{
    internal static GeofenceDto ToDto(Geofence g) => new(
        g.Id,
        g.TenantId,
        g.Name,
        g.Description,
        ToGeoJson(g.Boundary),
        g.IsActive,
        g.MaxSpeedKmh,
        g.AllowedFrom?.ToString("HH:mm"),
        g.AllowedTo?.ToString("HH:mm"),
        g.Direction.ToString(),
        g.CreatedAt,
        g.UpdatedAt);

    internal static ViolationDto ToDto(GeofenceViolation v) => new(
        v.Id,
        v.TenantId,
        v.GeofenceId,
        v.VehicleId,
        v.DriverId,
        v.ViolationType.ToString(),
        v.Position.Y,   // Y = latitude
        v.Position.X,   // X = longitude
        v.ActualSpeedKmh,
        v.LimitSpeedKmh,
        v.OccurredAt);

    private static GeoJsonPolygonDto ToGeoJson(Polygon polygon)
    {
        var coords = polygon.ExteriorRing.Coordinates
            .Select(c => new[] { c.X, c.Y })
            .ToArray();

        return new GeoJsonPolygonDto("Polygon", new[] { coords });
    }

    internal static Polygon FromGeoJson(double[][][] rings, GeometryFactory factory)
    {
        var exteriorCoords = rings[0]
            .Select(c => new Coordinate(c[0], c[1]))
            .ToArray();

        return factory.CreatePolygon(exteriorCoords);
    }
}
