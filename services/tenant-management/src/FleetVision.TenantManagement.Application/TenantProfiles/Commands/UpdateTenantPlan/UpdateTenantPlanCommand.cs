using FleetVision.TenantManagement.Application.DTOs;
using FleetVision.TenantManagement.Domain.Enums;
using MediatR;

namespace FleetVision.TenantManagement.Application.TenantProfiles.Commands.UpdateTenantPlan;

public sealed record UpdateTenantPlanCommand(Guid TenantId, PlanTier NewPlan) : IRequest<TenantProfileDto>;
