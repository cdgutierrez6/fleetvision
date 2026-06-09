using FleetVision.Geofencing.Application.Common;
using FleetVision.Geofencing.Application.DTOs;
using FleetVision.Geofencing.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.Geofencing.Application.Violations.Queries;

public sealed record ListViolationsQuery(
    Guid TenantId,
    Guid GeofenceId,
    int Page,
    int PageSize,
    Guid? VehicleId = null,
    ViolationType? ViolationType = null,
    DateTime? From = null,
    DateTime? To = null) : IRequest<PagedResult<ViolationDto>>;

public sealed class ListViolationsQueryHandler
    : IRequestHandler<ListViolationsQuery, PagedResult<ViolationDto>>
{
    private readonly IGeofencingDbContext _db;

    public ListViolationsQueryHandler(IGeofencingDbContext db) => _db = db;

    public async Task<PagedResult<ViolationDto>> Handle(ListViolationsQuery query, CancellationToken ct)
    {
        var baseQuery = _db.Violations
            .AsNoTracking()
            .Where(v => v.TenantId == query.TenantId && v.GeofenceId == query.GeofenceId);

        if (query.VehicleId.HasValue)
            baseQuery = baseQuery.Where(v => v.VehicleId == query.VehicleId.Value);

        if (query.ViolationType.HasValue)
            baseQuery = baseQuery.Where(v => v.ViolationType == query.ViolationType.Value);

        if (query.From.HasValue)
            baseQuery = baseQuery.Where(v => v.OccurredAt >= query.From.Value);

        if (query.To.HasValue)
            baseQuery = baseQuery.Where(v => v.OccurredAt <= query.To.Value);

        baseQuery = baseQuery.OrderByDescending(v => v.OccurredAt);

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<ViolationDto>(
            items.Select(GeofencingMappings.ToDto).ToList(),
            query.Page,
            query.PageSize,
            total);
    }
}
