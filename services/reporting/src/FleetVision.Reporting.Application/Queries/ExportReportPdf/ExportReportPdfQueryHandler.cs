using FleetVision.Reporting.Application.Common.Interfaces;
using FleetVision.Reporting.Application.Queries.GetFleetKpis;
using MediatR;

namespace FleetVision.Reporting.Application.Queries.ExportReportPdf;

public sealed class ExportReportPdfQueryHandler : IRequestHandler<ExportReportPdfQuery, byte[]>
{
    private readonly ITimeSeriesReader  _timeSeries;
    private readonly IViolationsReader  _violations;
    private readonly IFleetStatusReader _fleet;
    private readonly IPdfGenerator      _pdf;

    public ExportReportPdfQueryHandler(
        ITimeSeriesReader  timeSeries,
        IViolationsReader  violations,
        IFleetStatusReader fleet,
        IPdfGenerator      pdf)
    {
        _timeSeries = timeSeries;
        _violations = violations;
        _fleet      = fleet;
        _pdf        = pdf;
    }

    public async Task<byte[]> Handle(ExportReportPdfQuery request, CancellationToken ct)
    {
        var (from, to) = GetFleetKpisQueryHandler.ParseRange(request.Range);

        var kpis = await _timeSeries.GetFleetKpisAsync(request.TenantId, from, to, ct);
        var violations = await _violations.GetSummaryAsync(request.TenantId, from, to, ct);
        var status = await _fleet.GetFleetStatusAsync(request.TenantId, ct);

        return await _pdf.GenerateFleetReportAsync(
            request.TenantName, from, to, kpis, violations, status, ct);
    }
}
