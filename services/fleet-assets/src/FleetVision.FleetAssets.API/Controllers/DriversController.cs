using FleetVision.FleetAssets.Application.Drivers.Commands;
using FleetVision.FleetAssets.Application.Drivers.Queries;
using FleetVision.FleetAssets.Application.DTOs;
using FleetVision.FleetAssets.Domain.Enums;
using FleetVision.FleetAssets.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetVision.FleetAssets.API.Controllers;

[ApiController]
[Route("drivers")]
[Authorize]
public sealed class DriversController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;

    public DriversController(IMediator mediator, ITenantContext tenantContext)
    {
        _mediator      = mediator;
        _tenantContext = tenantContext;
    }

    private Guid TenantId => _tenantContext.TenantId
        ?? throw new InvalidOperationException("X-Tenant-Id header is required.");

    // GET /drivers?page=1&pageSize=20
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DriverDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListDriversQuery(TenantId, page, pageSize), ct);
        return Ok(result);
    }

    // POST /drivers
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,FleetAdmin")]
    [ProducesResponseType(typeof(DriverDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateDriverRequest request, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CreateDriverCommand(
            TenantId, request.FullName, request.LicenseNumber, request.Phone, request.Email), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    // GET /drivers/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DriverDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetDriverQuery(id, TenantId), ct);
        return Ok(result);
    }

    // PUT /drivers/{id}
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,FleetAdmin")]
    [ProducesResponseType(typeof(DriverDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateDriverRequest request, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new UpdateDriverCommand(
            id, TenantId, request.FullName, request.LicenseNumber,
            request.Phone, request.Email, request.Status), ct);
        return Ok(result);
    }

    // DELETE /drivers/{id}
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,FleetAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _mediator.Send(new DeleteDriverCommand(id, TenantId), ct);
        return NoContent();
    }
}

public sealed record CreateDriverRequest(
    string FullName, string LicenseNumber, string? Phone = null, string? Email = null);

public sealed record UpdateDriverRequest(
    string FullName, string LicenseNumber, string? Phone, string? Email, DriverStatus Status);
