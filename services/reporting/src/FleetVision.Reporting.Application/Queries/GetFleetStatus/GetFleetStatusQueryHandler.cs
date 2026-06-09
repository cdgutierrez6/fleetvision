using FleetVision.Reporting.Application.Common.Dtos;
using FleetVision.Reporting.Application.Common.Interfaces;
using MediatR;

namespace FleetVision.Reporting.Application.Queries.GetFleetStatus;

public sealed class GetFleetStatusQueryHandler
    : IRequestHandler<GetFleetStatusQuery, FleetStatusDto>
{
    private readonly IFleetStatusReader _fleet;

    public GetFleetStatusQueryHandler(IFleetStatusReader fleet)
        => _fleet = fleet;

    public Task<FleetStatusDto> Handle(GetFleetStatusQuery request, CancellationToken ct)
        => _fleet.GetFleetStatusAsync(request.TenantId, ct);
}
