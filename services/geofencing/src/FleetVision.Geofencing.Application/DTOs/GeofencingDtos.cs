namespace FleetVision.Geofencing.Application.DTOs;

public sealed record GeofenceDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Description,
    GeoJsonPolygonDto Boundary,
    bool IsActive,
    int? MaxSpeedKmh,
    string? AllowedFrom,
    string? AllowedTo,
    string Direction,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record GeoJsonPolygonDto(
    string Type,
    double[][][] Coordinates);

public sealed record ViolationDto(
    Guid Id,
    Guid TenantId,
    Guid GeofenceId,
    Guid VehicleId,
    Guid? DriverId,
    string ViolationType,
    double Latitude,
    double Longitude,
    double? ActualSpeedKmh,
    int? LimitSpeedKmh,
    DateTime OccurredAt);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record TelemetryPositionEvent(
    Guid TenantId,
    Guid VehicleId,
    Guid? DriverId,
    double Longitude,
    double Latitude,
    double? SpeedKmh,
    DateTime Timestamp);

public sealed record EvaluationResult(
    int ViolationsDetected,
    IReadOnlyList<ViolationDto> Violations);
