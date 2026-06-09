using FleetVision.Reporting.Application.Common.Dtos;

namespace FleetVision.Reporting.Application.Common.Interfaces;

public interface ITimeSeriesReader
{
    Task<FleetKpisDto> GetFleetKpisAsync(
        Guid tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);

    Task<IReadOnlyList<PositionPointDto>> GetVehicleHistoryAsync(
        Guid tenantId, Guid vehicleId, DateTimeOffset from, CancellationToken ct);
}
