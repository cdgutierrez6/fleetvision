using FleetVision.Geofencing.Application.Common;
using FleetVision.Geofencing.Application.DTOs;
using FleetVision.Geofencing.Domain.Enums;
using FleetVision.Geofencing.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace FleetVision.Geofencing.Application.Geofences.Commands;

public sealed record UpdateGeofenceCommand(
    Guid Id,
    Guid TenantId,
    string Name,
    double[][][] Coordinates,
    string? Description,
    int? MaxSpeedKmh,
    string? AllowedFrom,
    string? AllowedTo,
    GeofenceDirection Direction) : IRequest<GeofenceDto>;

public sealed class UpdateGeofenceCommandHandler : IRequestHandler<UpdateGeofenceCommand, GeofenceDto>
{
    private readonly IGeofencingDbContext _db;
    private readonly GeometryFactory _geometryFactory;

    public UpdateGeofenceCommandHandler(IGeofencingDbContext db, GeometryFactory geometryFactory)
    {
        _db              = db;
        _geometryFactory = geometryFactory;
    }

    public async Task<GeofenceDto> Handle(UpdateGeofenceCommand command, CancellationToken ct)
    {
        var geofence = await _db.Geofences
            .FirstOrDefaultAsync(g => g.Id == command.Id && g.TenantId == command.TenantId, ct)
            ?? throw new GeofenceNotFoundException(command.Id);

        var nameConflict = await _db.Geofences
            .AnyAsync(g => g.TenantId == command.TenantId && g.Name == command.Name.Trim() && g.Id != command.Id, ct);
        if (nameConflict) throw new GeofenceNameAlreadyExistsException(command.Name);

        var polygon = GeofencingMappings.FromGeoJson(command.Coordinates, _geometryFactory);

        TimeOnly? allowedFrom = command.AllowedFrom is null ? null : TimeOnly.Parse(command.AllowedFrom);
        TimeOnly? allowedTo   = command.AllowedTo   is null ? null : TimeOnly.Parse(command.AllowedTo);

        geofence.Update(command.Name, polygon, command.Description,
            command.MaxSpeedKmh, allowedFrom, allowedTo, command.Direction);

        await _db.SaveChangesAsync(ct);
        return GeofencingMappings.ToDto(geofence);
    }
}

public sealed class UpdateGeofenceCommandValidator : AbstractValidator<UpdateGeofenceCommand>
{
    public UpdateGeofenceCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Coordinates).NotNull()
            .Must(c => c.Length >= 1 && c[0].Length >= 4)
            .WithMessage("Polygon must have at least 3 distinct vertices.");
        RuleFor(x => x.MaxSpeedKmh).GreaterThan(0).When(x => x.MaxSpeedKmh.HasValue);
    }
}
