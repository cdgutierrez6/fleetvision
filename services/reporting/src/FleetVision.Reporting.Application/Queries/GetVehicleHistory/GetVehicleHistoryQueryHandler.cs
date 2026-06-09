using FleetVision.Reporting.Application.Common.Dtos;
using FleetVision.Reporting.Application.Common.Interfaces;
using MediatR;

namespace FleetVision.Reporting.Application.Queries.GetVehicleHistory;

public sealed class GetVehicleHistoryQueryHandler
    : IRequestHandler<GetVehicleHistoryQuery, IReadOnlyList<PositionPointDto>>
{
    private const int MaxHours = 168; // 7 days cap
    private readonly ITimeSeriesReader _timeSeries;

    public GetVehicleHistoryQueryHandler(ITimeSeriesReader timeSeries)
        => _timeSeries = timeSeries;

    public async Task<IReadOnlyList<PositionPointDto>> Handle(
        GetVehicleHistoryQuery request, CancellationToken ct)
    {
        var hours = Math.Clamp(request.Hours, 1, MaxHours);
        var from  = DateTimeOffset.UtcNow.AddHours(-hours);
        return await _timeSeries.GetVehicleHistoryAsync(request.TenantId, request.VehicleId, from, ct);
    }
}
