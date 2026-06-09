using FleetVision.Reporting.Application.Common.Dtos;
using MediatR;

namespace FleetVision.Reporting.Application.Queries.GetFleetStatus;

public sealed record GetFleetStatusQuery(Guid TenantId) : IRequest<FleetStatusDto>;
