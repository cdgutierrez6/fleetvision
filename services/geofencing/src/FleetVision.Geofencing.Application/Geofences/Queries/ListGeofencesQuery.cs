using FleetVision.Geofencing.Application.Common;
using FleetVision.Geofencing.Application.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.Geofencing.Application.Geofences.Queries;

public sealed record ListGeofencesQuery(Guid TenantId, int Page, int PageSize) : IRequest<PagedResult<GeofenceDto>>;

public sealed class ListGeofencesQueryHandler : IRequestHandler<ListGeofencesQuery, PagedResult<GeofenceDto>>
{
    private readonly IGeofencingDbContext _db;

    public ListGeofencesQueryHandler(IGeofencingDbContext db) => _db = db;

    public async Task<PagedResult<GeofenceDto>> Handle(ListGeofencesQuery query, CancellationToken ct)
    {
        var baseQuery = _db.Geofences
            .AsNoTracking()
            .Where(g => g.TenantId == query.TenantId)
            .OrderBy(g => g.CreatedAt);

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<GeofenceDto>(
            items.Select(GeofencingMappings.ToDto).ToList(),
            query.Page,
            query.PageSize,
            total);
    }
}
