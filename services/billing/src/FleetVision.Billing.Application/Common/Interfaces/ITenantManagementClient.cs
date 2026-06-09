using FleetVision.Billing.Domain.Enums;

namespace FleetVision.Billing.Application.Common.Interfaces;

public interface ITenantManagementClient
{
    Task UpdateTenantPlanAsync(Guid tenantId, PlanTier plan, CancellationToken ct);
}
