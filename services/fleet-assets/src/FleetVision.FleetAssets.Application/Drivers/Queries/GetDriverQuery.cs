using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Application.DTOs;
using FleetVision.FleetAssets.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.FleetAssets.Application.Drivers.Queries;

public sealed record GetDriverQuery(Guid Id, Guid TenantId) : IRequest<DriverDto>;

public sealed class GetDriverQueryHandler : IRequestHandler<GetDriverQuery, DriverDto>
{
    private readonly IFleetAssetsDbContext _db;

    public GetDriverQueryHandler(IFleetAssetsDbContext db) => _db = db;

    public async Task<DriverDto> Handle(GetDriverQuery query, CancellationToken ct)
    {
        var driver = await _db.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == query.Id && d.TenantId == query.TenantId, ct)
            ?? throw new DriverNotFoundException(query.Id);

        return FleetAssetsMappings.ToDto(driver);
    }
}
