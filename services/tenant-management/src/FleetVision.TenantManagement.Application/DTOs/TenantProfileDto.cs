namespace FleetVision.TenantManagement.Application.DTOs;

public sealed record TenantProfileDto(
    Guid Id,
    Guid TenantId,
    string CompanyName,
    string Slug,
    string Plan,
    int MaxVehicles,
    int MaxUsers,
    string BillingEmail,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record TenantLimitsDto(
    Guid TenantId,
    string Plan,
    int MaxVehicles,
    int MaxUsers,
    bool IsActive);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
