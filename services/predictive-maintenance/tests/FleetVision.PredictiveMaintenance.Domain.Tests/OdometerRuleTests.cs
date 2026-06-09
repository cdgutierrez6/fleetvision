using FleetVision.PredictiveMaintenance.Domain.Entities;
using FleetVision.PredictiveMaintenance.Domain.Rules;
using FleetVision.PredictiveMaintenance.Domain.ValueObjects;
using FluentAssertions;

namespace FleetVision.PredictiveMaintenance.Domain.Tests;

public sealed class OdometerRuleTests
{
    private readonly OdometerRule _rule = new();
    private static readonly Guid TenantId  = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly MaintenanceThresholds Thresholds = new(OdometerKm: 10_000m);

    private static TelemetryReading Reading() => new(
        TenantId, VehicleId, 4.7109, -74.0721, 60f, 0.5m, null, DateTime.UtcNow);

    [Fact]
    public void Below_threshold_no_rule_triggered()
    {
        var odometer = OdometerSnapshot.FromKm(9_999m);
        _rule.IsSatisfiedBy(Reading(), odometer, Thresholds, null).Should().BeFalse();
    }

    [Fact]
    public void At_threshold_rule_triggered()
    {
        var odometer = OdometerSnapshot.FromKm(10_000m);
        _rule.IsSatisfiedBy(Reading(), odometer, Thresholds, null).Should().BeTrue();
    }

    [Fact]
    public void Above_threshold_rule_triggered()
    {
        var odometer = OdometerSnapshot.FromKm(10_500m);
        _rule.IsSatisfiedBy(Reading(), odometer, Thresholds, null).Should().BeTrue();
    }

    [Fact]
    public void Unknown_odometer_never_triggers()
    {
        _rule.IsSatisfiedBy(Reading(), OdometerSnapshot.Unknown, Thresholds, null).Should().BeFalse();
    }

    [Fact]
    public void Created_record_has_correct_type_and_trigger()
    {
        var odometer = OdometerSnapshot.FromKm(10_001m);
        var record   = _rule.CreateRecord(Reading(), odometer, Thresholds);

        record.RecordType.Should().Be("SCHEDULED");
        record.TriggeredBy.Should().Be("ODOMETER");
        record.OdometerKm.Should().Be(10_001m);
        record.ThresholdKm.Should().Be(10_000m);
        record.TenantId.Should().Be(TenantId);
        record.VehicleId.Should().Be(VehicleId);
    }
}
