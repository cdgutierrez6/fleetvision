using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Application.DTOs;
using FleetVision.FleetAssets.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.FleetAssets.Application.Fleets.Queries;

public sealed record GetFleetQuery(Guid Id, Guid TenantId) : IRequest<FleetDto>;

public sealed class GetFleetQueryHandler : IRequestHandler<GetFleetQuery, FleetDto>
{
    private readonly IFleetAssetsDbContext _db;

    public GetFleetQueryHandler(IFleetAssetsDbContext db) => _db = db;

    public async Task<FleetDto> Handle(GetFleetQuery query, CancellationToken ct)
    {
        var fleet = await _db.Fleets
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == query.Id && f.TenantId == query.TenantId, ct)
            ?? throw new FleetNotFoundException(query.Id);

        return FleetAssetsMappings.ToDto(fleet);
    }
}
