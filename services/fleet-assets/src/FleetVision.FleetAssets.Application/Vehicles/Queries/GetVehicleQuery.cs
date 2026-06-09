using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Application.DTOs;
using FleetVision.FleetAssets.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.FleetAssets.Application.Vehicles.Queries;

public sealed record GetVehicleQuery(Guid Id, Guid TenantId) : IRequest<VehicleDto>;

public sealed class GetVehicleQueryHandler : IRequestHandler<GetVehicleQuery, VehicleDto>
{
    private readonly IFleetAssetsDbContext _db;

    public GetVehicleQueryHandler(IFleetAssetsDbContext db) => _db = db;

    public async Task<VehicleDto> Handle(GetVehicleQuery query, CancellationToken ct)
    {
        var vehicle = await _db.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == query.Id && v.TenantId == query.TenantId, ct)
            ?? throw new VehicleNotFoundException(query.Id);

        return FleetAssetsMappings.ToDto(vehicle);
    }
}
