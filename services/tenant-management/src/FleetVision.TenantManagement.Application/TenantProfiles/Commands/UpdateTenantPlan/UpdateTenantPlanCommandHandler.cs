using FleetVision.TenantManagement.Application.Common.Interfaces;
using FleetVision.TenantManagement.Application.DTOs;
using FleetVision.TenantManagement.Application.TenantProfiles.Commands.CreateTenantProfile;
using FleetVision.TenantManagement.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetVision.TenantManagement.Application.TenantProfiles.Commands.UpdateTenantPlan;

public sealed class UpdateTenantPlanCommandHandler
    : IRequestHandler<UpdateTenantPlanCommand, TenantProfileDto>
{
    private readonly ITenantManagementDbContext _db;
    private readonly ILogger<UpdateTenantPlanCommandHandler> _logger;

    public UpdateTenantPlanCommandHandler(
        ITenantManagementDbContext db,
        ILogger<UpdateTenantPlanCommandHandler> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<TenantProfileDto> Handle(
        UpdateTenantPlanCommand request,
        CancellationToken cancellationToken)
    {
        var profile = await _db.TenantProfiles
            .FirstOrDefaultAsync(t => t.TenantId == request.TenantId, cancellationToken)
            ?? throw new TenantProfileNotFoundException(request.TenantId);

        profile.ChangePlan(request.NewPlan);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated tenant {TenantId} plan to {Plan}", request.TenantId, request.NewPlan);

        return CreateTenantProfileCommandHandler.ToDto(profile);
    }
}
