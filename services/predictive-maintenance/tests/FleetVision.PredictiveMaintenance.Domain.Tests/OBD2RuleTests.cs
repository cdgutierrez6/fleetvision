using FleetVision.PredictiveMaintenance.Domain.Entities;
using FleetVision.PredictiveMaintenance.Domain.Rules;
using FleetVision.PredictiveMaintenance.Domain.ValueObjects;
using FluentAssertions;

namespace FleetVision.PredictiveMaintenance.Domain.Tests;

public sealed class OBD2RuleTests
{
    private static readonly Guid TenantId  = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly MaintenanceThresholds Thresholds = new();
    private static readonly OdometerSnapshot Odometer = OdometerSnapshot.FromKm(5_000m);

    private static TelemetryReading Reading(string? code) => new(
        TenantId, VehicleId, 4.7109, -74.0721, 60f, 0.5m, code, DateTime.UtcNow);

    [Theory]
    [InlineData("P0300")]
    [InlineData("P0301")]
    [InlineData("P0420")]
    [InlineData("P0562")]
    [InlineData("U0100")]
    public void Critical_codes_trigger_critical_rule(string code)
    {
        var rule = new OBD2CriticalRule();
        rule.IsSatisfiedBy(Reading(code), Odometer, Thresholds, null).Should().BeTrue();
    }

    [Fact]
    public void Null_obd2_does_not_trigger_critical_rule()
    {
        new OBD2CriticalRule()
            .IsSatisfiedBy(Reading(null), Odometer, Thresholds, null).Should().BeFalse();
    }

    [Theory]
    [InlineData("P1100")]
    [InlineData("C0035")]
    [InlineData("B1200")]
    public void Warning_codes_trigger_warning_rule(string code)
    {
        var rule = new OBD2WarningRule();
        rule.IsSatisfiedBy(Reading(code), Odometer, Thresholds, null).Should().BeTrue();
    }

    [Theory]
    [InlineData("P0300")]
    [InlineData("P0420")]
    public void Critical_codes_do_not_trigger_warning_rule(string code)
    {
        new OBD2WarningRule()
            .IsSatisfiedBy(Reading(code), Odometer, Thresholds, null).Should().BeFalse();
    }

    [Fact]
    public void Critical_record_has_correct_type()
    {
        var rule   = new OBD2CriticalRule();
        var record = rule.CreateRecord(Reading("P0300"), Odometer, Thresholds);

        record.RecordType.Should().Be("CRITICAL_ALERT");
        record.TriggeredBy.Should().Be("OBD2_CODE");
        record.Obd2Code.Should().Be("P0300");
    }

    [Fact]
    public void Warning_record_has_scheduled_type()
    {
        var rule   = new OBD2WarningRule();
        var record = rule.CreateRecord(Reading("P1100"), Odometer, Thresholds);

        record.RecordType.Should().Be("SCHEDULED");
        record.TriggeredBy.Should().Be("OBD2_CODE");
    }
}
