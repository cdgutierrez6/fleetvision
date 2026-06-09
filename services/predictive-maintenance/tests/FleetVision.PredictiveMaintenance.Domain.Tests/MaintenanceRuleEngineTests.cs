using FleetVision.PredictiveMaintenance.Domain.Entities;
using FleetVision.PredictiveMaintenance.Domain.Rules;
using FleetVision.PredictiveMaintenance.Domain.Services;
using FleetVision.PredictiveMaintenance.Domain.ValueObjects;
using FluentAssertions;

namespace FleetVision.PredictiveMaintenance.Domain.Tests;

public sealed class MaintenanceRuleEngineTests
{
    private static readonly MaintenanceRuleEngine Engine = new(new IMaintenanceRule[]
    {
        new OBD2CriticalRule(),
        new OBD2WarningRule(),
        new OdometerRule(),
        new TimeBasedRule(),
    });

    private static TelemetryReading Reading(string? obd2 = null, decimal distanceKm = 0.5m) => new(
        Guid.NewGuid(), Guid.NewGuid(), 4.7, -74.0, 60f, distanceKm, obd2, DateTime.UtcNow);

    [Fact]
    public void No_rules_triggered_for_normal_telemetry()
    {
        var records = Engine.Evaluate(
            Reading(), OdometerSnapshot.FromKm(1_000m), new MaintenanceThresholds(), null);
        records.Should().BeEmpty();
    }

    [Fact]
    public void Critical_obd2_produces_one_critical_record()
    {
        var records = Engine.Evaluate(
            Reading("P0300"), OdometerSnapshot.FromKm(1_000m), new MaintenanceThresholds(), null);

        records.Should().HaveCount(1);
        records[0].RecordType.Should().Be("CRITICAL_ALERT");
    }

    [Fact]
    public void Odometer_threshold_produces_scheduled_record()
    {
        var records = Engine.Evaluate(
            Reading(), OdometerSnapshot.FromKm(10_001m), new MaintenanceThresholds(10_000m), null);

        records.Should().HaveCount(1);
        records[0].TriggeredBy.Should().Be("ODOMETER");
    }

    [Fact]
    public void Critical_obd2_and_odometer_produce_two_records()
    {
        var records = Engine.Evaluate(
            Reading("P0420"), OdometerSnapshot.FromKm(10_001m), new MaintenanceThresholds(10_000m), null);

        records.Should().HaveCount(2);
        records.Should().Contain(r => r.RecordType == "CRITICAL_ALERT");
        records.Should().Contain(r => r.TriggeredBy == "ODOMETER");
    }

    [Fact]
    public void Time_based_rule_triggers_after_threshold_days()
    {
        var lastMaintenance = DateTime.UtcNow.AddDays(-181);
        var records = Engine.Evaluate(
            Reading(), OdometerSnapshot.Unknown, new MaintenanceThresholds(TimeBasedDays: 180), lastMaintenance);

        records.Should().Contain(r => r.TriggeredBy == "TIME_BASED");
    }

    [Fact]
    public void Time_based_rule_does_not_trigger_without_last_maintenance()
    {
        var records = Engine.Evaluate(
            Reading(), OdometerSnapshot.Unknown, new MaintenanceThresholds(TimeBasedDays: 180), null);

        records.Should().NotContain(r => r.TriggeredBy == "TIME_BASED");
    }
}
