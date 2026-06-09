using FleetVision.Reporting.Application.Common.Dtos;

namespace FleetVision.Reporting.Application.Common.Interfaces;

public interface IPdfGenerator
{
    Task<byte[]> GenerateFleetReportAsync(
        string         tenantName,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        FleetKpisDto          kpis,
        ViolationsSummaryDto  violations,
        FleetStatusDto        status,
        CancellationToken ct);
}
