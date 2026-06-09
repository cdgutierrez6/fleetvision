using FleetVision.TenantManagement.Application.DTOs;
using MediatR;

namespace FleetVision.TenantManagement.Application.TenantProfiles.Queries.GetTenantProfile;

public sealed record GetTenantProfileQuery(Guid TenantId) : IRequest<TenantProfileDto>;
