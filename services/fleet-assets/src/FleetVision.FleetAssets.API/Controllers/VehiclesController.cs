using FleetVision.FleetAssets.Application.DTOs;
using FleetVision.FleetAssets.Application.VehicleAssignments.Commands;
using FleetVision.FleetAssets.Application.VehicleAssignments.Queries;
using FleetVision.FleetAssets.Application.Vehicles.Commands;
using FleetVision.FleetAssets.Application.Vehicles.Queries;
using FleetVision.FleetAssets.Domain.Enums;
using FleetVision.FleetAssets.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetVision.FleetAssets.API.Controllers;

[ApiController]
[Route("vehicles")]
[Authorize]
public sealed class VehiclesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;

    public VehiclesController(IMediator mediator, ITenantContext tenantContext)
    {
        _mediator      = mediator;
        _tenantContext = tenantContext;
    }

    private Guid TenantId => _tenantContext.TenantId
        ?? throw new InvalidOperationException("X-Tenant-Id header is required.");

    // GET /vehicles?page=1&pageSize=20&fleetId={id}
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<VehicleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? fleetId = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListVehiclesQuery(TenantId, page, pageSize, fleetId), ct);
        return Ok(result);
    }

    // POST /vehicles
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,FleetAdmin")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateVehicleRequest request, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CreateVehicleCommand(
            TenantId, request.FleetId, request.Plate, request.Vin,
            request.Brand, request.Model, request.Year, request.OdometerKm), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    // GET /vehicles/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetVehicleQuery(id, TenantId), ct);
        return Ok(result);
    }

    // PUT /vehicles/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,FleetAdmin")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateVehicleRequest request, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new UpdateVehicleCommand(
            id, TenantId, request.Plate, request.Brand, request.Model,
            request.Year, request.OdometerKm, request.Status), ct);
        return Ok(result);
    }

    // DELETE /vehicles/{id}
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,FleetAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _mediator.Send(new DeleteVehicleCommand(id, TenantId), ct);
        return NoContent();
    }

    // PATCH /vehicles/{id}/position
    [HttpPatch("{id:guid}/position")]
    [Authorize(Roles = "SuperAdmin,FleetAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePosition(
        Guid id, [FromBody] UpdatePositionRequest request, CancellationToken ct = default)
    {
        await _mediator.Send(
            new UpdateVehiclePositionCommand(id, TenantId, request.Longitude, request.Latitude), ct);
        return NoContent();
    }

    // ─── Vehicle Assignments ──────────────────────────────────────────────────

    // GET /vehicles/{id}/assignments
    [HttpGet("{id:guid}/assignments")]
    [ProducesResponseType(typeof(PagedResult<AssignmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAssignments(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListVehicleAssignmentsQuery(TenantId, id, page, pageSize), ct);
        return Ok(result);
    }

    // POST /vehicles/{id}/assignments
    [HttpPost("{id:guid}/assignments")]
    [Authorize(Roles = "SuperAdmin,FleetAdmin")]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateAssignment(
        Guid id, [FromBody] CreateAssignmentRequest request, CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new CreateVehicleAssignmentCommand(TenantId, id, request.DriverId), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    // DELETE /vehicles/{id}/assignments/current
    [HttpDelete("{id:guid}/assignments/current")]
    [Authorize(Roles = "SuperAdmin,FleetAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CloseAssignment(Guid id, CancellationToken ct = default)
    {
        await _mediator.Send(new CloseVehicleAssignmentCommand(TenantId, id), ct);
        return NoContent();
    }
}

public sealed record CreateVehicleRequest(
    Guid FleetId, string Plate, string? Vin,
    string Brand, string Model, int Year, int OdometerKm = 0);

public sealed record UpdateVehicleRequest(
    string Plate, string Brand, string Model,
    int Year, int OdometerKm, VehicleStatus Status);

public sealed record UpdatePositionRequest(double Longitude, double Latitude);
public sealed record CreateAssignmentRequest(Guid DriverId);
