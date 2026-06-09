using FleetVision.Reporting.Application.Common.Dtos;
using MediatR;

namespace FleetVision.Reporting.Application.Queries.GetVehicleHistory;

public sealed record GetVehicleHistoryQuery(
    Guid TenantId,
    Guid VehicleId,
    int  Hours) : IRequest<IReadOnlyList<PositionPointDto>>;
