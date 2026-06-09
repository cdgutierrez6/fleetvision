using FleetVision.Reporting.Application.Common.Dtos;
using FleetVision.Reporting.Application.Common.Interfaces;
using MediatR;

namespace FleetVision.Reporting.Application.Queries.GetFleetKpis;

public sealed class GetFleetKpisQueryHandler : IRequestHandler<GetFleetKpisQuery, FleetKpisDto>
{
    private readonly ITimeSeriesReader _timeSeries;

    public GetFleetKpisQueryHandler(ITimeSeriesReader timeSeries)
        => _timeSeries = timeSeries;

    public async Task<FleetKpisDto> Handle(GetFleetKpisQuery request, CancellationToken ct)
    {
        var (from, to) = ParseRange(request.Range);
        return await _timeSeries.GetFleetKpisAsync(request.TenantId, from, to, ct);
    }

    internal static (DateTimeOffset From, DateTimeOffset To) ParseRange(string range)
    {
        var to   = DateTimeOffset.UtcNow;
        var days = range switch
        {
            "7d"  => 7,
            "90d" => 90,
            _     => 30,
        };
        return (to.AddDays(-days), to);
    }
}
