using FleetVision.Reporting.Application.Common.Dtos;
using FleetVision.Reporting.Application.Queries.ExportReportPdf;
using FleetVision.Reporting.Application.Queries.GetFleetKpis;
using FleetVision.Reporting.Application.Queries.GetFleetStatus;
using FleetVision.Reporting.Application.Queries.GetVehicleHistory;
using FleetVision.Reporting.Application.Queries.GetViolationsSummary;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FleetVision.Reporting.API.Controllers;

[ApiController]
[Route("reports")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReportsController(IMediator mediator) => _mediator = mediator;

    private Guid TenantId => HttpContext.Items["TenantId"] is Guid id
        ? id
        : throw new InvalidOperationException("TenantId missing from context.");

    // GET /reports/fleet-kpis?range=7d|30d|90d
    [HttpGet("fleet-kpis")]
    [ProducesResponseType(typeof(FleetKpisDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> FleetKpis(
        [FromQuery] string range = "30d", CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetFleetKpisQuery(TenantId, range), ct);
        return Ok(result);
    }

    // GET /reports/violations-summary?range=7d|30d|90d
    [HttpGet("violations-summary")]
    [ProducesResponseType(typeof(ViolationsSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ViolationsSummary(
        [FromQuery] string range = "30d", CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetViolationsSummaryQuery(TenantId, range), ct);
        return Ok(result);
    }

    // GET /reports/vehicle-history/{vehicleId}?hours=24
    [HttpGet("vehicle-history/{vehicleId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<PositionPointDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VehicleHistory(
        Guid vehicleId,
        [FromQuery] int hours = 24,
        CancellationToken ct = default)
    {
        if (hours < 1 || hours > 168)
            return BadRequest(new { error = "hours must be between 1 and 168." });

        var result = await _mediator.Send(
            new GetVehicleHistoryQuery(TenantId, vehicleId, hours), ct);
        return Ok(result);
    }

    // GET /reports/fleet-status
    [HttpGet("fleet-status")]
    [ProducesResponseType(typeof(FleetStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> FleetStatus(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetFleetStatusQuery(TenantId), ct);
        return Ok(result);
    }

    // POST /reports/export/pdf
    [HttpPost("export/pdf")]
    [EnableRateLimiting("pdf-export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ExportPdf(
        [FromBody] ExportPdfRequest request, CancellationToken ct = default)
    {
        var bytes = await _mediator.Send(
            new ExportReportPdfQuery(TenantId, request.TenantName, request.Range), ct);

        // X-Content-Type-Options prevents browsers from MIME-sniffing the PDF response.
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        return File(bytes, "application/pdf",
            $"fleetvision-report-{request.Range}-{DateTimeOffset.UtcNow:yyyyMMdd}.pdf");
    }
}

public sealed record ExportPdfRequest(
    string TenantName,
    string Range = "30d");
