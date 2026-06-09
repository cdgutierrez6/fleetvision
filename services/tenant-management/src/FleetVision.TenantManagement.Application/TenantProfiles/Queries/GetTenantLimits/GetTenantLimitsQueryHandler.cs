using FleetVision.TenantManagement.Application.Common.Interfaces;
using FleetVision.TenantManagement.Application.DTOs;
using FleetVision.TenantManagement.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.TenantManagement.Application.TenantProfiles.Queries.GetTenantLimits;

public sealed class GetTenantLimitsQueryHandler : IRequestHandler<GetTenantLimitsQuery, TenantLimitsDto>
{
    private readonly ITenantManagementDbContext _db;

    public GetTenantLimitsQueryHandler(ITenantManagementDbContext db) => _db = db;

    public async Task<TenantLimitsDto> Handle(
        GetTenantLimitsQuery request,
        CancellationToken cancellationToken)
    {
        var profile = await _db.TenantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == request.TenantId, cancellationToken)
            ?? throw new TenantProfileNotFoundException(request.TenantId);

        return new TenantLimitsDto(
            profile.TenantId,
            profile.Plan.ToString(),
            profile.MaxVehicles,
            profile.MaxUsers,
            profile.IsActive);
    }
}
