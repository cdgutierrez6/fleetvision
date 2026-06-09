using FleetVision.PredictiveMaintenance.Domain.Entities;
using FleetVision.PredictiveMaintenance.Domain.Rules;
using FleetVision.PredictiveMaintenance.Domain.ValueObjects;

namespace FleetVision.PredictiveMaintenance.Domain.Services;

public sealed class MaintenanceRuleEngine
{
    private readonly IReadOnlyList<IMaintenanceRule> _rules;

    public MaintenanceRuleEngine(IReadOnlyList<IMaintenanceRule> rules)
        => _rules = rules;

    public IReadOnlyList<MaintenanceRecord> Evaluate(
        TelemetryReading reading,
        OdometerSnapshot odometer,
        MaintenanceThresholds thresholds,
        DateTime? lastMaintenanceAt)
    {
        var records = new List<MaintenanceRecord>();

        foreach (var rule in _rules)
        {
            if (rule.IsSatisfiedBy(reading, odometer, thresholds, lastMaintenanceAt))
                records.Add(rule.CreateRecord(reading, odometer, thresholds));
        }

        return records;
    }
}
