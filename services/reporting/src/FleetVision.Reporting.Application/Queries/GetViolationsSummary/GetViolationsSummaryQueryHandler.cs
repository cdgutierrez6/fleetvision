using FleetVision.Reporting.Application.Common.Dtos;
using FleetVision.Reporting.Application.Common.Interfaces;
using FleetVision.Reporting.Application.Queries.GetFleetKpis;
using MediatR;

namespace FleetVision.Reporting.Application.Queries.GetViolationsSummary;

public sealed class GetViolationsSummaryQueryHandler
    : IRequestHandler<GetViolationsSummaryQuery, ViolationsSummaryDto>
{
    private readonly IViolationsReader _violations;

    public GetViolationsSummaryQueryHandler(IViolationsReader violations)
        => _violations = violations;

    public async Task<ViolationsSummaryDto> Handle(
        GetViolationsSummaryQuery request, CancellationToken ct)
    {
        var (from, to) = GetFleetKpisQueryHandler.ParseRange(request.Range);
        return await _violations.GetSummaryAsync(request.TenantId, from, to, ct);
    }
}
