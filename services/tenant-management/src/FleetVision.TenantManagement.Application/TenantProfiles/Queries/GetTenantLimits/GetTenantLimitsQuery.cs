using FleetVision.TenantManagement.Application.DTOs;
using MediatR;

namespace FleetVision.TenantManagement.Application.TenantProfiles.Queries.GetTenantLimits;

public sealed record GetTenantLimitsQuery(Guid TenantId) : IRequest<TenantLimitsDto>;
