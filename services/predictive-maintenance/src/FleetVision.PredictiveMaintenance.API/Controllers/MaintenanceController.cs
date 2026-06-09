using FleetVision.PredictiveMaintenance.Application.Commands;
using FleetVision.PredictiveMaintenance.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FleetVision.PredictiveMaintenance.API.Controllers;

[ApiController]
[Route("maintenance")]
[Authorize]
public sealed class MaintenanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public MaintenanceController(IMediator mediator) => _mediator = mediator;

    private Guid TenantId => Guid.Parse(User.FindFirstValue("tenant_id")
        ?? throw new UnauthorizedAccessException("tenant_id claim required."));

    [HttpGet("vehicles/{vehicleId:guid}/records")]
    public async Task<IActionResult> GetRecords(
        Guid vehicleId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(new { error = "page must be ≥1; pageSize between 1 and 100." });

        var records = await _mediator.Send(
            new GetMaintenanceRecordsQuery(vehicleId, TenantId, page, pageSize), ct);

        return Ok(new { data = records, page, pageSize });
    }

    [HttpPost("records/{id:guid}/complete")]
    public async Task<IActionResult> CompleteRecord(Guid id, CancellationToken ct = default)
    {
        var success = await _mediator.Send(new CompleteMaintenanceCommand(id, TenantId), ct);
        return success ? NoContent() : NotFound(new { error = "Maintenance record not found." });
    }
}
