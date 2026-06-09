using FleetVision.PredictiveMaintenance.Domain.Entities;
using FleetVision.PredictiveMaintenance.Domain.Interfaces;
using FleetVision.PredictiveMaintenance.Domain.Services;
using FleetVision.PredictiveMaintenance.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FleetVision.PredictiveMaintenance.Application.Services;

public sealed class MaintenanceOrchestrator
{
    private readonly MaintenanceRuleEngine       _ruleEngine;
    private readonly IMaintenanceRepository      _repository;
    private readonly IOdometerCache              _odometerCache;
    private readonly IMaintenanceOutboxEnqueuer  _outbox;
    private readonly MaintenanceOptions          _options;
    private readonly ILogger<MaintenanceOrchestrator> _logger;

    public MaintenanceOrchestrator(
        MaintenanceRuleEngine engine,
        IMaintenanceRepository repository,
        IOdometerCache odometerCache,
        IMaintenanceOutboxEnqueuer outbox,
        IOptions<MaintenanceOptions> options,
        ILogger<MaintenanceOrchestrator> logger)
    {
        _ruleEngine    = engine;
        _repository    = repository;
        _odometerCache = odometerCache;
        _outbox        = outbox;
        _options       = options.Value;
        _logger        = logger;
    }

    public async Task ProcessAsync(TelemetryReading reading, long kafkaOffset, CancellationToken ct)
    {
        var odometer = await _odometerCache.GetAndIncrementAsync(
            reading.TenantId, reading.VehicleId, reading.DistanceKm, kafkaOffset, ct);

        var thresholds     = new MaintenanceThresholds(_options.OdometerThresholdKm, _options.TimeBasedDays);
        var lastMaintained = await _repository.GetLastMaintenanceAtAsync(reading.VehicleId, reading.TenantId, ct);
        var records        = _ruleEngine.Evaluate(reading, odometer, thresholds, lastMaintained);

        foreach (var record in records)
        {
            await _repository.AddAsync(record, ct);

            if (record.RecordType == "CRITICAL_ALERT")
            {
                _outbox.EnqueueAlert(record);
                _logger.LogWarning(
                    "Vehicle {VehicleId}: CRITICAL alert — OBD2 {Code}",
                    reading.VehicleId, record.Obd2Code);
            }
            else
            {
                _outbox.EnqueueScheduled(record);
                _logger.LogInformation(
                    "Vehicle {VehicleId}: scheduled maintenance triggered by {TriggeredBy}",
                    reading.VehicleId, record.TriggeredBy);
            }
        }

        if (records.Count > 0)
        {
            await _repository.SaveChangesAsync(ct);

            if (records.Any(r => r.TriggeredBy == "ODOMETER"))
                await _odometerCache.ResetAsync(reading.TenantId, reading.VehicleId, ct);
        }
    }
}

public sealed class MaintenanceOptions
{
    public decimal OdometerThresholdKm { get; set; } = 10_000m;
    public int     TimeBasedDays       { get; set; } = 180;
}
