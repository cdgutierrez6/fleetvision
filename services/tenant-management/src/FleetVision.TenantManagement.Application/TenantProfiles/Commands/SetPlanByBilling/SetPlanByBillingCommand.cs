using FleetVision.TenantManagement.Domain.Enums;
using MediatR;

namespace FleetVision.TenantManagement.Application.TenantProfiles.Commands.SetPlanByBilling;

public sealed record SetPlanByBillingCommand(Guid TenantId, PlanTier NewPlan) : IRequest;
