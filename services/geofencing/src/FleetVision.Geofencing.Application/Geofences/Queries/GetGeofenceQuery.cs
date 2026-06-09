using FleetVision.Geofencing.Application.Common;
using FleetVision.Geofencing.Application.DTOs;
using FleetVision.Geofencing.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.Geofencing.Application.Geofences.Queries;

public sealed record GetGeofenceQuery(Guid Id, Guid TenantId) : IRequest<GeofenceDto>;

public sealed class GetGeofenceQueryHandler : IRequestHandler<GetGeofenceQuery, GeofenceDto>
{
    private readonly IGeofencingDbContext _db;

    public GetGeofenceQueryHandler(IGeofencingDbContext db) => _db = db;

    public async Task<GeofenceDto> Handle(GetGeofenceQuery query, CancellationToken ct)
    {
        var geofence = await _db.Geofences
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == query.Id && g.TenantId == query.TenantId, ct)
            ?? throw new GeofenceNotFoundException(query.Id);

        return GeofencingMappings.ToDto(geofence);
    }
}
