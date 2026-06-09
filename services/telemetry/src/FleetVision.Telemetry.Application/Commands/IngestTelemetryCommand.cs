using FleetVision.Telemetry.Application.Common;
using FleetVision.Telemetry.Domain.Entities;
using FluentValidation;
using MediatR;

namespace FleetVision.Telemetry.Application.Commands;

public sealed record IngestTelemetryCommand(
    Guid VehicleId,
    Guid TenantId,
    Guid? DriverId,
    long TimestampUnixMs,
    double Latitude,
    double Longitude,
    float? SpeedKmh = null,
    float? HeadingDeg = null,
    float? AccuracyM = null,
    float? Hdop = null,
    int? SatelliteCount = null,
    float? FuelPct = null,
    bool? EngineOn = null,
    IReadOnlyList<string>? Obd2Codes = null,
    float? OdometerKm = null) : IRequest<IngestTelemetryResult>;

public sealed record IngestTelemetryResult(bool Accepted, string PositionKey);

public sealed class IngestTelemetryCommandHandler
    : IRequestHandler<IngestTelemetryCommand, IngestTelemetryResult>
{
    private readonly ITelemetryWriter _writer;
    private readonly IPositionCache _cache;

    public IngestTelemetryCommandHandler(ITelemetryWriter writer, IPositionCache cache)
    {
        _writer = writer;
        _cache  = cache;
    }

    public async Task<IngestTelemetryResult> Handle(IngestTelemetryCommand cmd, CancellationToken ct)
    {
        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(cmd.TimestampUnixMs).UtcDateTime;

        var position = VehiclePosition.Create(
            vehicleId:      cmd.VehicleId,
            tenantId:       cmd.TenantId,
            timestamp:      timestamp,
            latitude:       cmd.Latitude,
            longitude:      cmd.Longitude,
            driverId:       cmd.DriverId,
            speedKmh:       cmd.SpeedKmh.HasValue ? (double?)cmd.SpeedKmh.Value : null,
            headingDeg:     cmd.HeadingDeg.HasValue ? (short?)((short)cmd.HeadingDeg.Value) : null,
            accuracyM:      cmd.AccuracyM.HasValue ? (double?)cmd.AccuracyM.Value : null,
            hdop:           cmd.Hdop.HasValue ? (double?)cmd.Hdop.Value : null,
            satelliteCount: cmd.SatelliteCount.HasValue ? (short?)((short)cmd.SatelliteCount.Value) : null,
            fuelPct:        cmd.FuelPct.HasValue ? (double?)cmd.FuelPct.Value : null,
            engineOn:       cmd.EngineOn,
            obd2Codes:      cmd.Obd2Codes?.ToArray(),
            odometerKm:     cmd.OdometerKm.HasValue ? (double?)cmd.OdometerKm.Value : null);

        // Persist + Outbox enqueue en una sola transacción DB (atomicidad del Outbox pattern).
        // Redis cache corre en paralelo — es best-effort, fallo no interrumpe el flujo.
        await Task.WhenAll(
            _writer.PersistAndEnqueueAsync(position, ct),
            _cache.SetAsync(position, ct));

        var positionKey = $"{position.VehicleId}::{cmd.TimestampUnixMs}";
        return new IngestTelemetryResult(Accepted: true, PositionKey: positionKey);
    }
}

public sealed class IngestTelemetryCommandValidator : AbstractValidator<IngestTelemetryCommand>
{
    public IngestTelemetryCommandValidator()
    {
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.TimestampUnixMs).GreaterThan(0);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.SpeedKmh).GreaterThanOrEqualTo(0).When(x => x.SpeedKmh.HasValue);
        RuleFor(x => x.FuelPct).InclusiveBetween(0, 100).When(x => x.FuelPct.HasValue);
    }
}
