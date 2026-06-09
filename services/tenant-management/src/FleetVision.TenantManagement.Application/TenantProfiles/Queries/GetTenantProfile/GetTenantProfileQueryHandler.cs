using FleetVision.TenantManagement.Application.Common.Interfaces;
using FleetVision.TenantManagement.Application.DTOs;
using FleetVision.TenantManagement.Application.TenantProfiles.Commands.CreateTenantProfile;
using FleetVision.TenantManagement.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.TenantManagement.Application.TenantProfiles.Queries.GetTenantProfile;

public sealed class GetTenantProfileQueryHandler : IRequestHandler<GetTenantProfileQuery, TenantProfileDto>
{
    private readonly ITenantManagementDbContext _db;

    public GetTenantProfileQueryHandler(ITenantManagementDbContext db) => _db = db;

    public async Task<TenantProfileDto> Handle(
        GetTenantProfileQuery request,
        CancellationToken cancellationToken)
    {
        var profile = await _db.TenantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == request.TenantId, cancellationToken)
            ?? throw new TenantProfileNotFoundException(request.TenantId);

        return CreateTenantProfileCommandHandler.ToDto(profile);
    }
}
