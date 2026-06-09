using FleetVision.Geofencing.Application.Common;
using FleetVision.Geofencing.Application.DTOs;
using FleetVision.Geofencing.Domain.Entities;
using FleetVision.Geofencing.Domain.Enums;
using FleetVision.Geofencing.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace FleetVision.Geofencing.Application.Geofences.Commands;

public sealed record CreateGeofenceCommand(
    Guid TenantId,
    string Name,
    double[][][] Coordinates,
    string? Description = null,
    int? MaxSpeedKmh = null,
    string? AllowedFrom = null,
    string? AllowedTo = null,
    GeofenceDirection Direction = GeofenceDirection.Both) : IRequest<GeofenceDto>;

public sealed class CreateGeofenceCommandHandler : IRequestHandler<CreateGeofenceCommand, GeofenceDto>
{
    private readonly IGeofencingDbContext _db;
    private readonly ITenantLimitsClient _limitsClient;
    private readonly GeometryFactory _geometryFactory;

    public CreateGeofenceCommandHandler(
        IGeofencingDbContext db,
        ITenantLimitsClient limitsClient,
        GeometryFactory geometryFactory)
    {
        _db              = db;
        _limitsClient    = limitsClient;
        _geometryFactory = geometryFactory;
    }

    public async Task<GeofenceDto> Handle(CreateGeofenceCommand command, CancellationToken ct)
    {
        var limits = await _limitsClient.GetLimitsAsync(command.TenantId, ct);
        var currentCount = await _db.Geofences
            .Where(g => g.TenantId == command.TenantId)
            .CountAsync(ct);

        if (currentCount >= limits.MaxGeofences)
            throw new GeofencePlanLimitExceededException(limits.MaxGeofences, limits.Plan);

        var nameExists = await _db.Geofences
            .AnyAsync(g => g.TenantId == command.TenantId && g.Name == command.Name.Trim(), ct);
        if (nameExists) throw new GeofenceNameAlreadyExistsException(command.Name);

        var polygon = GeofencingMappings.FromGeoJson(command.Coordinates, _geometryFactory);

        TimeOnly? allowedFrom = command.AllowedFrom is null ? null : TimeOnly.Parse(command.AllowedFrom);
        TimeOnly? allowedTo   = command.AllowedTo   is null ? null : TimeOnly.Parse(command.AllowedTo);

        var geofence = Geofence.Create(
            command.TenantId,
            command.Name,
            polygon,
            command.Description,
            command.MaxSpeedKmh,
            allowedFrom,
            allowedTo,
            command.Direction);

        _db.Geofences.Add(geofence);
        await _db.SaveChangesAsync(ct);
        return GeofencingMappings.ToDto(geofence);
    }
}

public sealed class CreateGeofenceCommandValidator : AbstractValidator<CreateGeofenceCommand>
{
    public CreateGeofenceCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Coordinates).NotNull()
            .Must(c => c.Length >= 1 && c[0].Length >= 4)
            .WithMessage("Polygon must have at least 3 distinct vertices (4 coordinates with closing point).");
        RuleFor(x => x.MaxSpeedKmh).GreaterThan(0).When(x => x.MaxSpeedKmh.HasValue);
        RuleFor(x => x.AllowedFrom)
            .Must(s => TimeOnly.TryParse(s, out _))
            .WithMessage("AllowedFrom must be a valid time (HH:mm).")
            .When(x => x.AllowedFrom is not null);
        RuleFor(x => x.AllowedTo)
            .Must(s => TimeOnly.TryParse(s, out _))
            .WithMessage("AllowedTo must be a valid time (HH:mm).")
            .When(x => x.AllowedTo is not null);
    }
}
