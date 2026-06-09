using FleetVision.Geofencing.Domain.Enums;
using FleetVision.Geofencing.Domain.Exceptions;
using NetTopologySuite.Geometries;

namespace FleetVision.Geofencing.Domain.Entities;

public sealed class Geofence
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Polygon Boundary { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public int? MaxSpeedKmh { get; private set; }
    public TimeOnly? AllowedFrom { get; private set; }
    public TimeOnly? AllowedTo { get; private set; }
    public GeofenceDirection Direction { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Geofence() { }

    public static Geofence Create(
        Guid tenantId,
        string name,
        Polygon boundary,
        string? description = null,
        int? maxSpeedKmh = null,
        TimeOnly? allowedFrom = null,
        TimeOnly? allowedTo = null,
        GeofenceDirection direction = GeofenceDirection.Both)
    {
        if (tenantId == Guid.Empty)          throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (name.Length > 100)               throw new ArgumentException("Name cannot exceed 100 characters.", nameof(name));

        ValidatePolygon(boundary);
        ValidateSpeedLimit(maxSpeedKmh);
        ValidateTimeWindow(allowedFrom, allowedTo);

        return new Geofence
        {
            Id          = Guid.NewGuid(),
            TenantId    = tenantId,
            Name        = name.Trim(),
            Description = description?.Trim(),
            Boundary    = boundary,
            IsActive    = true,
            MaxSpeedKmh = maxSpeedKmh,
            AllowedFrom = allowedFrom,
            AllowedTo   = allowedTo,
            Direction   = direction,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };
    }

    public void Update(
        string name,
        Polygon boundary,
        string? description,
        int? maxSpeedKmh,
        TimeOnly? allowedFrom,
        TimeOnly? allowedTo,
        GeofenceDirection direction)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (name.Length > 100)               throw new ArgumentException("Name cannot exceed 100 characters.", nameof(name));

        ValidatePolygon(boundary);
        ValidateSpeedLimit(maxSpeedKmh);
        ValidateTimeWindow(allowedFrom, allowedTo);

        Name        = name.Trim();
        Description = description?.Trim();
        Boundary    = boundary;
        MaxSpeedKmh = maxSpeedKmh;
        AllowedFrom = allowedFrom;
        AllowedTo   = allowedTo;
        Direction   = direction;
        UpdatedAt   = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive  = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive  = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool ContainsPoint(Point point) => Boundary.Contains(point);

    private static void ValidatePolygon(Polygon polygon)
    {
        if (polygon is null)
            throw new InvalidPolygonException("Polygon cannot be null");

        if (!polygon.IsValid)
            throw new InvalidPolygonException("Polygon geometry is not valid (self-intersecting or malformed)");

        // Minimum 4 coordinates (3 unique + closing point to form a triangle)
        if (polygon.ExteriorRing.NumPoints < 4)
            throw new InvalidPolygonException("Polygon must have at least 3 distinct vertices");

        if (polygon.SRID != 4326)
            throw new InvalidPolygonException("Polygon must use SRID 4326 (WGS84)");
    }

    private static void ValidateSpeedLimit(int? maxSpeedKmh)
    {
        if (maxSpeedKmh.HasValue && maxSpeedKmh.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSpeedKmh), "Speed limit must be positive.");
    }

    private static void ValidateTimeWindow(TimeOnly? allowedFrom, TimeOnly? allowedTo)
    {
        if (allowedFrom.HasValue != allowedTo.HasValue)
            throw new ArgumentException("Both AllowedFrom and AllowedTo must be set together or both null.");
    }
}
