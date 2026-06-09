using FleetVision.TenantManagement.Application.DTOs;
using FleetVision.TenantManagement.Domain.Enums;
using MediatR;

namespace FleetVision.TenantManagement.Application.TenantProfiles.Commands.CreateTenantProfile;

public sealed record CreateTenantProfileCommand(
    Guid TenantId,
    string CompanyName,
    string Slug,
    string BillingEmail,
    PlanTier Plan = PlanTier.Free) : IRequest<TenantProfileDto>;
