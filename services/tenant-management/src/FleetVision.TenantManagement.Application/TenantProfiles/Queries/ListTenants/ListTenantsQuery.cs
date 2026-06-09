using FleetVision.TenantManagement.Application.DTOs;
using MediatR;

namespace FleetVision.TenantManagement.Application.TenantProfiles.Queries.ListTenants;

public sealed record ListTenantsQuery(int Page = 1, int PageSize = 20) : IRequest<PagedResult<TenantProfileDto>>;
