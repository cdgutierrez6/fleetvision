using FleetVision.TenantManagement.Application.Common.Interfaces;
using FleetVision.TenantManagement.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetVision.TenantManagement.Application.TenantProfiles.Commands.SetPlanByBilling;

public sealed class SetPlanByBillingCommandHandler : IRequestHandler<SetPlanByBillingCommand>
{
    private readonly ITenantManagementDbContext _db;
    private readonly ILogger<SetPlanByBillingCommandHandler> _logger;

    public SetPlanByBillingCommandHandler(
        ITenantManagementDbContext db,
        ILogger<SetPlanByBillingCommandHandler> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task Handle(SetPlanByBillingCommand request, CancellationToken cancellationToken)
    {
        var profile = await _db.TenantProfiles
            .FirstOrDefaultAsync(t => t.TenantId == request.TenantId, cancellationToken)
            ?? throw new TenantProfileNotFoundException(request.TenantId);

        profile.SetPlanByBilling(request.NewPlan);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Billing set tenant {TenantId} plan to {Plan}", request.TenantId, request.NewPlan);
    }
}
