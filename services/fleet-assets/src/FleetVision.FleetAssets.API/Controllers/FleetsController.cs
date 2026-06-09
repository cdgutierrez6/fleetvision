using FleetVision.FleetAssets.Application.DTOs;
using FleetVision.FleetAssets.Application.Fleets.Commands;
using FleetVision.FleetAssets.Application.Fleets.Queries;
using FleetVision.FleetAssets.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetVision.FleetAssets.API.Controllers;

[ApiController]
[Route("fleets")]
[Authorize]
public sealed class FleetsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;

    public FleetsController(IMediator mediator, ITenantContext tenantContext)
    {
        _mediator      = mediator;
        _tenantContext = tenantContext;
    }

    private Guid TenantId => _tenantContext.TenantId
        ?? throw new InvalidOperationException("X-Tenant-Id header is required.");

    // GET /fleets?page=1&pageSize=20
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<FleetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListFleetsQuery(TenantId, page, pageSize), ct);
        return Ok(result);
    }

    // POST /fleets
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,FleetAdmin")]
    [ProducesResponseType(typeof(FleetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateFleetRequest request, CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new CreateFleetCommand(TenantId, request.Name, request.Description), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    // GET /fleets/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FleetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetFleetQuery(id, TenantId), ct);
        return Ok(result);
    }

    // PUT /fleets/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,FleetAdmin")]
    [ProducesResponseType(typeof(FleetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateFleetRequest request, CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new UpdateFleetCommand(id, TenantId, request.Name, request.Description), ct);
        return Ok(result);
    }

    // DELETE /fleets/{id}
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,FleetAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _mediator.Send(new DeleteFleetCommand(id, TenantId), ct);
        return NoContent();
    }
}

public sealed record CreateFleetRequest(string Name, string? Description);
public sealed record UpdateFleetRequest(string Name, string? Description);
