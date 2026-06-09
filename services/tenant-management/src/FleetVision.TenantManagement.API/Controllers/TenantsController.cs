using FleetVision.TenantManagement.Application.DTOs;
using FleetVision.TenantManagement.Application.TenantProfiles.Commands.CreateTenantProfile;
using FleetVision.TenantManagement.Application.TenantProfiles.Commands.SetPlanByBilling;
using FleetVision.TenantManagement.Application.TenantProfiles.Commands.SetTenantActiveStatus;
using FleetVision.TenantManagement.Application.TenantProfiles.Commands.UpdateTenantPlan;
using FleetVision.TenantManagement.Application.TenantProfiles.Queries.GetTenantLimits;
using FleetVision.TenantManagement.Application.TenantProfiles.Queries.GetTenantProfile;
using FleetVision.TenantManagement.Application.TenantProfiles.Queries.ListTenants;
using FleetVision.TenantManagement.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace FleetVision.TenantManagement.API.Controllers;

[ApiController]
[Route("tenants")]
[Authorize]
public sealed class TenantsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _config;

    public TenantsController(IMediator mediator, IConfiguration config)
    {
        _mediator = mediator;
        _config   = config;
    }

    // ─── GET /tenants?page=1&pageSize=20 ─────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(PagedResult<TenantProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListTenantsQuery(page, pageSize), ct);
        return Ok(result);
    }

    // ─── POST /tenants ────────────────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(TenantProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantProfileRequest request,
        CancellationToken ct = default)
    {
        var command = new CreateTenantProfileCommand(
            request.TenantId,
            request.CompanyName,
            request.Slug,
            request.BillingEmail,
            request.Plan);

        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Get), new { tenantId = result.TenantId }, result);
    }

    // ─── GET /tenants/{tenantId} ──────────────────────────────────────────────
    [HttpGet("{tenantId:guid}")]
    [ProducesResponseType(typeof(TenantProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid tenantId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetTenantProfileQuery(tenantId), ct);
        return Ok(result);
    }

    // ─── PATCH /tenants/{tenantId}/plan ──────────────────────────────────────
    [HttpPatch("{tenantId:guid}/plan")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(typeof(TenantProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdatePlan(
        Guid tenantId,
        [FromBody] UpdateTenantPlanRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new UpdateTenantPlanCommand(tenantId, request.Plan), ct);
        return Ok(result);
    }

    // ─── POST /tenants/{tenantId}/activate ───────────────────────────────────
    [HttpPost("{tenantId:guid}/activate")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid tenantId, CancellationToken ct = default)
    {
        await _mediator.Send(new SetTenantActiveStatusCommand(tenantId, IsActive: true), ct);
        return NoContent();
    }

    // ─── POST /tenants/{tenantId}/deactivate ─────────────────────────────────
    [HttpPost("{tenantId:guid}/deactivate")]
    [Authorize(Roles = "SuperAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid tenantId, CancellationToken ct = default)
    {
        await _mediator.Send(new SetTenantActiveStatusCommand(tenantId, IsActive: false), ct);
        return NoContent();
    }

    // ─── GET /tenants/{tenantId}/limits ──────────────────────────────────────
    // Internal endpoint — service-to-service only. Not exposed via YARP Gateway.
    // Requires X-Internal-Key to prevent unauthenticated tenant enumeration.
    [HttpGet("{tenantId:guid}/limits")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    [ProducesResponseType(typeof(TenantLimitsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLimits(
        Guid tenantId,
        [FromHeader(Name = "X-Internal-Key")] string? internalKey,
        CancellationToken ct = default)
    {
        var expectedKey = _config["InternalApiKey"];
        if (string.IsNullOrEmpty(expectedKey) || string.IsNullOrEmpty(internalKey))
            return Unauthorized();

        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        var actualBytes   = Encoding.UTF8.GetBytes(internalKey);
        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
            return Unauthorized();

        var result = await _mediator.Send(new GetTenantLimitsQuery(tenantId), ct);
        return Ok(result);
    }

    // ─── PUT /internal/tenants/{tenantId}/plan ────────────────────────────────
    // Service-to-service endpoint for Billing Service only.
    // Not exposed via YARP Gateway (/internal/* is not routed externally).
    [HttpPut("/internal/tenants/{tenantId:guid}/plan")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPlanByBilling(
        Guid tenantId,
        [FromBody] SetPlanByBillingRequest request,
        [FromHeader(Name = "X-Internal-Key")] string? internalKey,
        CancellationToken ct = default)
    {
        var expectedKey = _config["InternalApiKey"];
        if (string.IsNullOrEmpty(expectedKey) || string.IsNullOrEmpty(internalKey))
            return Unauthorized();

        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        var actualBytes   = Encoding.UTF8.GetBytes(internalKey);
        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
            return Unauthorized();

        await _mediator.Send(new SetPlanByBillingCommand(tenantId, request.Plan), ct);
        return NoContent();
    }
}

// ─── Request bodies ───────────────────────────────────────────────────────────

public sealed record CreateTenantProfileRequest(
    Guid TenantId,
    string CompanyName,
    string Slug,
    string BillingEmail,
    PlanTier Plan = PlanTier.Free);

public sealed record UpdateTenantPlanRequest(PlanTier Plan);
public sealed record SetPlanByBillingRequest(PlanTier Plan);
