using FleetVision.PredictiveMaintenance.Domain.Entities;
using FleetVision.PredictiveMaintenance.Domain.Rules;
using FleetVision.PredictiveMaintenance.Domain.ValueObjects;
using FluentAssertions;

namespace FleetVision.PredictiveMaintenance.Domain.Tests;

public sealed class TimeBasedRuleTests
{
    private readonly TimeBasedRule _rule = new();
    private static readonly Guid TenantId  = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly MaintenanceThresholds Thresholds = new(TimeBasedDays: 180);

    private static TelemetryReading Reading(DateTime timestamp) => new(
        TenantId, VehicleId, 4.71, -74.07, 60f, 0.5m, null, timestamp);

    [Fact]
    public void No_last_maintenance_rule_not_triggered()
    {
        var reading = Reading(DateTime.UtcNow);
        _rule.IsSatisfiedBy(reading, OdometerSnapshot.FromKm(100m), Thresholds, lastMaintenanceAt: null)
             .Should().BeFalse();
    }

    [Fact]
    public void Last_maintenance_today_rule_not_triggered()
    {
        var now = DateTime.UtcNow;
        var reading = Reading(now);
        _rule.IsSatisfiedBy(reading, OdometerSnapshot.FromKm(100m), Thresholds, lastMaintenanceAt: now)
             .Should().BeFalse();
    }

    [Fact]
    public void Last_maintenance_exactly_at_threshold_triggers()
    {
        var lastMaintenance = DateTime.UtcNow.AddDays(-180);
        var reading = Reading(DateTime.UtcNow);
        _rule.IsSatisfiedBy(reading, OdometerSnapshot.FromKm(100m), Thresholds, lastMaintenanceAt: lastMaintenance)
             .Should().BeTrue();
    }

    [Fact]
    public void Last_maintenance_over_threshold_triggers()
    {
        var lastMaintenance = DateTime.UtcNow.AddDays(-200);
        var reading = Reading(DateTime.UtcNow);
        _rule.IsSatisfiedBy(reading, OdometerSnapshot.FromKm(100m), Thresholds, lastMaintenanceAt: lastMaintenance)
             .Should().BeTrue();
    }

    [Fact]
    public void Last_maintenance_just_under_threshold_does_not_trigger()
    {
        var lastMaintenance = DateTime.UtcNow.AddDays(-179);
        var reading = Reading(DateTime.UtcNow);
        _rule.IsSatisfiedBy(reading, OdometerSnapshot.FromKm(100m), Thresholds, lastMaintenanceAt: lastMaintenance)
             .Should().BeFalse();
    }

    [Fact]
    public void Created_record_has_time_based_trigger_and_note()
    {
        var reading = Reading(DateTime.UtcNow);
        var record  = _rule.CreateRecord(reading, OdometerSnapshot.FromKm(500m), Thresholds);

        record.RecordType.Should().Be("SCHEDULED");
        record.TriggeredBy.Should().Be("TIME_BASED");
        record.TenantId.Should().Be(TenantId);
        record.VehicleId.Should().Be(VehicleId);
        record.OdometerKm.Should().Be(500m);
        record.Notes.Should().Contain("180");
    }

    [Fact]
    public void Created_record_with_unknown_odometer_has_null_odometer()
    {
        var reading = Reading(DateTime.UtcNow);
        var record  = _rule.CreateRecord(reading, OdometerSnapshot.Unknown, Thresholds);

        record.OdometerKm.Should().BeNull();
    }
}
