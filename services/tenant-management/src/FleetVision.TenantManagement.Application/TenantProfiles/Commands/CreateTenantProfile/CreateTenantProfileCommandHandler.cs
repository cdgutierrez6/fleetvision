using FleetVision.TenantManagement.Application.Common.Interfaces;
using FleetVision.TenantManagement.Application.DTOs;
using FleetVision.TenantManagement.Domain.Entities;
using FleetVision.TenantManagement.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetVision.TenantManagement.Application.TenantProfiles.Commands.CreateTenantProfile;

public sealed class CreateTenantProfileCommandHandler
    : IRequestHandler<CreateTenantProfileCommand, TenantProfileDto>
{
    private readonly ITenantManagementDbContext _db;
    private readonly ILogger<CreateTenantProfileCommandHandler> _logger;

    public CreateTenantProfileCommandHandler(
        ITenantManagementDbContext db,
        ILogger<CreateTenantProfileCommandHandler> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<TenantProfileDto> Handle(
        CreateTenantProfileCommand request,
        CancellationToken cancellationToken)
    {
        var exists = await _db.TenantProfiles
            .AnyAsync(t => t.TenantId == request.TenantId, cancellationToken);

        if (exists)
            throw new TenantProfileAlreadyExistsException(request.TenantId);

        var profile = TenantProfile.Create(
            request.TenantId,
            request.CompanyName,
            request.Slug,
            request.BillingEmail,
            request.Plan);

        _db.TenantProfiles.Add(profile);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created tenant profile {ProfileId} for tenant {TenantId} on plan {Plan}",
            profile.Id, profile.TenantId, profile.Plan);

        return ToDto(profile);
    }

    internal static TenantProfileDto ToDto(Domain.Entities.TenantProfile p) => new(
        p.Id, p.TenantId, p.CompanyName, p.Slug, p.Plan.ToString(),
        p.MaxVehicles, p.MaxUsers, p.BillingEmail, p.IsActive, p.CreatedAt, p.UpdatedAt);
}
