using FleetVision.TenantManagement.Application.Common.Interfaces;
using FleetVision.TenantManagement.Application.DTOs;
using FleetVision.TenantManagement.Application.TenantProfiles.Commands.CreateTenantProfile;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.TenantManagement.Application.TenantProfiles.Queries.ListTenants;

public sealed class ListTenantsQueryHandler
    : IRequestHandler<ListTenantsQuery, PagedResult<TenantProfileDto>>
{
    private readonly ITenantManagementDbContext _db;

    public ListTenantsQueryHandler(ITenantManagementDbContext db) => _db = db;

    public async Task<PagedResult<TenantProfileDto>> Handle(
        ListTenantsQuery request,
        CancellationToken cancellationToken)
    {
        var page     = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var total = await _db.TenantProfiles.CountAsync(cancellationToken);

        // Materialize first — ToDto uses a C# method not translatable to SQL
        var entities = await _db.TenantProfiles
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = entities.Select(CreateTenantProfileCommandHandler.ToDto).ToList();

        return new PagedResult<TenantProfileDto>(items, total, page, pageSize);
    }
}
