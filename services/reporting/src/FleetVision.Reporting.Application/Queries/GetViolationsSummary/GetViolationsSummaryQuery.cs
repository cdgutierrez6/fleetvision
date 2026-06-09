using FleetVision.Reporting.Application.Common.Dtos;
using MediatR;

namespace FleetVision.Reporting.Application.Queries.GetViolationsSummary;

public sealed record GetViolationsSummaryQuery(
    Guid   TenantId,
    string Range) : IRequest<ViolationsSummaryDto>;
