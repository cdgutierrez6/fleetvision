using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Application.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.FleetAssets.Application.Drivers.Queries;

public sealed record ListDriversQuery(Guid TenantId, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<DriverDto>>;

public sealed class ListDriversQueryHandler : IRequestHandler<ListDriversQuery, PagedResult<DriverDto>>
{
    private readonly IFleetAssetsDbContext _db;

    public ListDriversQueryHandler(IFleetAssetsDbContext db) => _db = db;

    public async Task<PagedResult<DriverDto>> Handle(ListDriversQuery query, CancellationToken ct)
    {
        var baseQuery = _db.Drivers
            .AsNoTracking()
            .Where(d => d.TenantId == query.TenantId);

        var total = await baseQuery.CountAsync(ct);

        var entities = await baseQuery
            .OrderByDescending(d => d.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<DriverDto>(
            entities.Select(FleetAssetsMappings.ToDto).ToList(),
            query.Page, query.PageSize, total);
    }
}
