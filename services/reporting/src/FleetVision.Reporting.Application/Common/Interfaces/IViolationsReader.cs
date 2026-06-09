using FleetVision.Reporting.Application.Common.Dtos;

namespace FleetVision.Reporting.Application.Common.Interfaces;

public interface IViolationsReader
{
    Task<ViolationsSummaryDto> GetSummaryAsync(
        Guid tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
