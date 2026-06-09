using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Application.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.FleetAssets.Application.Vehicles.Queries;

public sealed record ListVehiclesQuery(Guid TenantId, int Page = 1, int PageSize = 20, Guid? FleetId = null)
    : IRequest<PagedResult<VehicleDto>>;

public sealed class ListVehiclesQueryHandler : IRequestHandler<ListVehiclesQuery, PagedResult<VehicleDto>>
{
    private readonly IFleetAssetsDbContext _db;

    public ListVehiclesQueryHandler(IFleetAssetsDbContext db) => _db = db;

    public async Task<PagedResult<VehicleDto>> Handle(ListVehiclesQuery query, CancellationToken ct)
    {
        var baseQuery = _db.Vehicles
            .AsNoTracking()
            .Where(v => v.TenantId == query.TenantId);

        if (query.FleetId.HasValue)
            baseQuery = baseQuery.Where(v => v.FleetId == query.FleetId.Value);

        var total = await baseQuery.CountAsync(ct);

        var entities = await baseQuery
            .OrderByDescending(v => v.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<VehicleDto>(
            entities.Select(FleetAssetsMappings.ToDto).ToList(),
            query.Page, query.PageSize, total);
    }
}
