using FleetVision.Reporting.Application.Common.Dtos;

namespace FleetVision.Reporting.Application.Common.Interfaces;

public interface IFleetStatusReader
{
    Task<FleetStatusDto> GetFleetStatusAsync(Guid tenantId, CancellationToken ct);
}
