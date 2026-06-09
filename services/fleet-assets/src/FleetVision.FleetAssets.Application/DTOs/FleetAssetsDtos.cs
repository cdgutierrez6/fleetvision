namespace FleetVision.FleetAssets.Application.DTOs;

public sealed record FleetDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record VehicleDto(
    Guid Id,
    Guid TenantId,
    Guid FleetId,
    string Plate,
    string? Vin,
    string Brand,
    string Model,
    int Year,
    int OdometerKm,
    string Status,
    double? Latitude,
    double? Longitude,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record DriverDto(
    Guid Id,
    Guid TenantId,
    string FullName,
    string LicenseNumber,
    string? Phone,
    string? Email,
    string Status,
    bool IsDeleted,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record AssignmentDto(
    Guid Id,
    Guid TenantId,
    Guid VehicleId,
    Guid DriverId,
    DateTime StartedAt,
    DateTime? EndedAt);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int Total);
