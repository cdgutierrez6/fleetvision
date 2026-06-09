using FleetVision.Reporting.Application.Common.Dtos;
using MediatR;

namespace FleetVision.Reporting.Application.Queries.GetFleetKpis;

public sealed record GetFleetKpisQuery(
    Guid   TenantId,
    string Range) : IRequest<FleetKpisDto>;
