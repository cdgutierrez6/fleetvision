using FleetVision.Geofencing.Application.Common;
using FleetVision.Geofencing.Application.DTOs;
using FleetVision.Geofencing.Domain.Entities;
using FleetVision.Geofencing.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace FleetVision.Geofencing.Application.TelemetryEvaluation;

public sealed record EvaluateTelemetryEventCommand(
    Guid TenantId,
    Guid VehicleId,
    Guid? DriverId,
    double Longitude,
    double Latitude,
    double? SpeedKmh,
    DateTime Timestamp) : IRequest<EvaluationResult>;

public sealed class EvaluateTelemetryEventCommandHandler
    : IRequestHandler<EvaluateTelemetryEventCommand, EvaluationResult>
{
    private readonly IGeofencingDbContext _db;
    private readonly GeometryFactory _geometryFactory;
    private readonly IViolationPublisher _publisher;

    public EvaluateTelemetryEventCommandHandler(
        IGeofencingDbContext db,
        GeometryFactory geometryFactory,
        IViolationPublisher publisher)
    {
        _db              = db;
        _geometryFactory = geometryFactory;
        _publisher       = publisher;
    }

    public async Task<EvaluationResult> Handle(EvaluateTelemetryEventCommand command, CancellationToken ct)
    {
        var vehiclePoint = _geometryFactory.CreatePoint(new Coordinate(command.Longitude, command.Latitude));
        var eventTime    = TimeOnly.FromDateTime(command.Timestamp);

        var activeGeofences = await _db.Geofences
            .Where(g => g.TenantId == command.TenantId && g.IsActive)
            .ToListAsync(ct);

        if (activeGeofences.Count == 0)
            return new EvaluationResult(0, Array.Empty<ViolationDto>());

        // Load all current states for this vehicle in one query
        var geofenceIds = activeGeofences.Select(g => g.Id).ToList();
        var states = await _db.VehicleGeofenceStates
            .Where(s => s.VehicleId == command.VehicleId && geofenceIds.Contains(s.GeofenceId))
            .ToListAsync(ct);

        var statesByGeofence = states.ToDictionary(s => s.GeofenceId);
        var violations       = new List<GeofenceViolation>();

        foreach (var geofence in activeGeofences)
        {
            var isNowInside = geofence.ContainsPoint(vehiclePoint);

            statesByGeofence.TryGetValue(geofence.Id, out var state);
            var wasInside = state?.IsInside ?? false;

            // ─── Zone transition violations ──────────────────────────────
            if (!wasInside && isNowInside && geofence.Direction != GeofenceDirection.ExitOnly)
            {
                violations.Add(GeofenceViolation.Create(
                    command.TenantId, geofence.Id, command.VehicleId, command.DriverId,
                    ViolationType.ZoneEntered, vehiclePoint));
            }
            else if (wasInside && !isNowInside && geofence.Direction != GeofenceDirection.EntryOnly)
            {
                violations.Add(GeofenceViolation.Create(
                    command.TenantId, geofence.Id, command.VehicleId, command.DriverId,
                    ViolationType.ZoneExited, vehiclePoint));
            }

            // ─── Speed violation (only inside zone) ──────────────────────
            if (isNowInside && geofence.MaxSpeedKmh.HasValue && command.SpeedKmh.HasValue
                && command.SpeedKmh.Value > geofence.MaxSpeedKmh.Value)
            {
                violations.Add(GeofenceViolation.Create(
                    command.TenantId, geofence.Id, command.VehicleId, command.DriverId,
                    ViolationType.SpeedExceeded, vehiclePoint,
                    command.SpeedKmh.Value, geofence.MaxSpeedKmh.Value));
            }

            // ─── Out-of-schedule violation (only inside zone) ────────────
            if (isNowInside && geofence.AllowedFrom.HasValue && geofence.AllowedTo.HasValue)
            {
                var inSchedule = IsInSchedule(eventTime, geofence.AllowedFrom.Value, geofence.AllowedTo.Value);
                if (!inSchedule)
                {
                    violations.Add(GeofenceViolation.Create(
                        command.TenantId, geofence.Id, command.VehicleId, command.DriverId,
                        ViolationType.OutOfSchedule, vehiclePoint));
                }
            }

            // ─── Update or create state ───────────────────────────────────
            if (state is null)
            {
                state = VehicleGeofenceState.Create(command.TenantId, command.VehicleId, geofence.Id, isNowInside);
                _db.VehicleGeofenceStates.Add(state);
            }
            else
            {
                state.UpdateState(isNowInside);
            }
        }

        if (violations.Count > 0)
        {
            _db.Violations.AddRange(violations);

            // Enqueue outbox events in the same SaveChangesAsync — atomicity guaranteed.
            // Publisher adds to _db.OutboxEvents without calling SaveChangesAsync itself.
            foreach (var violation in violations)
            {
                var geofenceName = activeGeofences.First(g => g.Id == violation.GeofenceId).Name;
                _publisher.Enqueue(violation, geofenceName);
            }
        }

        await _db.SaveChangesAsync(ct);

        return new EvaluationResult(
            violations.Count,
            violations.Select(GeofencingMappings.ToDto).ToList());
    }

    private static bool IsInSchedule(TimeOnly eventTime, TimeOnly from, TimeOnly to)
    {
        // Handles overnight windows: e.g. 22:00 → 06:00
        if (from <= to)
            return eventTime >= from && eventTime <= to;

        return eventTime >= from || eventTime <= to;
    }
}

public sealed class EvaluateTelemetryEventCommandValidator
    : AbstractValidator<EvaluateTelemetryEventCommand>
{
    public EvaluateTelemetryEventCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.SpeedKmh).GreaterThanOrEqualTo(0).When(x => x.SpeedKmh.HasValue);
    }
}
