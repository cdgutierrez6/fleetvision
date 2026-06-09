using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Application.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.FleetAssets.Application.Fleets.Queries;

public sealed record ListFleetsQuery(Guid TenantId, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<FleetDto>>;

public sealed class ListFleetsQueryHandler : IRequestHandler<ListFleetsQuery, PagedResult<FleetDto>>
{
    private readonly IFleetAssetsDbContext _db;

    public ListFleetsQueryHandler(IFleetAssetsDbContext db) => _db = db;

    public async Task<PagedResult<FleetDto>> Handle(ListFleetsQuery query, CancellationToken ct)
    {
        var baseQuery = _db.Fleets
            .AsNoTracking()
            .Where(f => f.TenantId == query.TenantId);

        var total = await baseQuery.CountAsync(ct);

        var entities = await baseQuery
            .OrderByDescending(f => f.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<FleetDto>(
            entities.Select(FleetAssetsMappings.ToDto).ToList(),
            query.Page, query.PageSize, total);
    }
}
