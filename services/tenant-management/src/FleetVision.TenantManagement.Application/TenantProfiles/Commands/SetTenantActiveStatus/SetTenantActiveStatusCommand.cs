using MediatR;

namespace FleetVision.TenantManagement.Application.TenantProfiles.Commands.SetTenantActiveStatus;

public sealed record SetTenantActiveStatusCommand(Guid TenantId, bool IsActive) : IRequest;
