using FleetVision.Geofencing.Application.DTOs;
using FleetVision.Geofencing.Application.Geofences.Commands;
using FleetVision.Geofencing.Application.Geofences.Queries;
using FleetVision.Geofencing.Application.TelemetryEvaluation;
using FleetVision.Geofencing.Application.Violations.Queries;
using FleetVision.Geofencing.Domain.Enums;
using FleetVision.Geofencing.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetVision.Geofencing.API.Controllers;

[ApiController]
[Route("geofences")]
[Authorize]
public sealed class GeofencesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;

    public GeofencesController(IMediator mediator, ITenantContext tenantContext)
    {
        _mediator      = mediator;
        _tenantContext = tenantContext;
    }

    private Guid TenantId => _tenantContext.TenantId
        ?? throw new InvalidOperationException("X-Tenant-Id header is required.");

    // GET /geofences?page=1&pageSize=20
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<GeofenceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListGeofencesQuery(TenantId, page, pageSize), ct);
        return Ok(result);
    }

    // POST /geofences
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,FleetAdmin")]
    [ProducesResponseType(typeof(GeofenceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateGeofenceRequest request, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CreateGeofenceCommand(
            TenantId,
            request.Name,
            request.Coordinates,
            request.Description,
            request.MaxSpeedKmh,
            request.AllowedFrom,
            request.AllowedTo,
            request.Direction), ct);

        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    // GET /geofences/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GeofenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetGeofenceQuery(id, TenantId), ct);
        return Ok(result);
    }

    // PUT /geofences/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,FleetAdmin")]
    [ProducesResponseType(typeof(GeofenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateGeofenceRequest request, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new UpdateGeofenceCommand(
            id, TenantId, request.Name, request.Coordinates, request.Description,
            request.MaxSpeedKmh, request.AllowedFrom, request.AllowedTo, request.Direction), ct);
        return Ok(result);
    }

    // DELETE /geofences/{id}
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,FleetAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _mediator.Send(new DeleteGeofenceCommand(id, TenantId), ct);
        return NoContent();
    }

    // GET /geofences/{id}/violations
    [HttpGet("{id:guid}/violations")]
    [ProducesResponseType(typeof(PagedResult<ViolationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListViolations(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? vehicleId = null,
        [FromQuery] ViolationType? violationType = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new ListViolationsQuery(TenantId, id, page, pageSize, vehicleId, violationType, from, to), ct);
        return Ok(result);
    }

    // POST /geofences/evaluate — internal endpoint for telemetry service
    [HttpPost("evaluate")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EvaluationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Evaluate(
        [FromBody] EvaluatePositionRequest request, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new EvaluateTelemetryEventCommand(
            request.TenantId,
            request.VehicleId,
            request.DriverId,
            request.Longitude,
            request.Latitude,
            request.SpeedKmh,
            request.Timestamp), ct);
        return Ok(result);
    }
}

public sealed record CreateGeofenceRequest(
    string Name,
    double[][][] Coordinates,
    string? Description = null,
    int? MaxSpeedKmh = null,
    string? AllowedFrom = null,
    string? AllowedTo = null,
    GeofenceDirection Direction = GeofenceDirection.Both);

public sealed record UpdateGeofenceRequest(
    string Name,
    double[][][] Coordinates,
    string? Description,
    int? MaxSpeedKmh,
    string? AllowedFrom,
    string? AllowedTo,
    GeofenceDirection Direction);

public sealed record EvaluatePositionRequest(
    Guid TenantId,
    Guid VehicleId,
    Guid? DriverId,
    double Longitude,
    double Latitude,
    double? SpeedKmh,
    DateTime Timestamp);
