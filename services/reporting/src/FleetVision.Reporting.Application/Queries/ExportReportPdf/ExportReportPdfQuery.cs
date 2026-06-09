using MediatR;

namespace FleetVision.Reporting.Application.Queries.ExportReportPdf;

public sealed record ExportReportPdfQuery(
    Guid   TenantId,
    string TenantName,
    string Range) : IRequest<byte[]>;
