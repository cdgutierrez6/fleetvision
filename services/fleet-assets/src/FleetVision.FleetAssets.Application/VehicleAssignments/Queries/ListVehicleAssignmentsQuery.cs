using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Application.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.FleetAssets.Application.VehicleAssignments.Queries;

public sealed record ListVehicleAssignmentsQuery(
    Guid TenantId, Guid VehicleId, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<AssignmentDto>>;

public sealed class ListVehicleAssignmentsQueryHandler
    : IRequestHandler<ListVehicleAssignmentsQuery, PagedResult<AssignmentDto>>
{
    private readonly IFleetAssetsDbContext _db;

    public ListVehicleAssignmentsQueryHandler(IFleetAssetsDbContext db) => _db = db;

    public async Task<PagedResult<AssignmentDto>> Handle(
        ListVehicleAssignmentsQuery query, CancellationToken ct)
    {
        var baseQuery = _db.VehicleAssignments
            .AsNoTracking()
            .Where(a => a.VehicleId == query.VehicleId && a.TenantId == query.TenantId);

        var total = await baseQuery.CountAsync(ct);

        var entities = await baseQuery
            .OrderByDescending(a => a.StartedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<AssignmentDto>(
            entities.Select(FleetAssetsMappings.ToDto).ToList(),
            query.Page, query.PageSize, total);
    }
}
