using FleetVision.TenantManagement.Application.Common.Interfaces;
using FleetVision.TenantManagement.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetVision.TenantManagement.Application.TenantProfiles.Commands.SetTenantActiveStatus;

public sealed class SetTenantActiveStatusCommandHandler : IRequestHandler<SetTenantActiveStatusCommand>
{
    private readonly ITenantManagementDbContext _db;
    private readonly ILogger<SetTenantActiveStatusCommandHandler> _logger;

    public SetTenantActiveStatusCommandHandler(
        ITenantManagementDbContext db,
        ILogger<SetTenantActiveStatusCommandHandler> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task Handle(SetTenantActiveStatusCommand request, CancellationToken cancellationToken)
    {
        var profile = await _db.TenantProfiles
            .FirstOrDefaultAsync(t => t.TenantId == request.TenantId, cancellationToken)
            ?? throw new TenantProfileNotFoundException(request.TenantId);

        if (request.IsActive) profile.Activate();
        else                  profile.Deactivate();

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Tenant {TenantId} {Status}", request.TenantId, request.IsActive ? "activated" : "deactivated");
    }
}
