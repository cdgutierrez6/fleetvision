using FleetVision.PredictiveMaintenance.Domain.Entities;
using FluentAssertions;

namespace FleetVision.PredictiveMaintenance.Domain.Tests;

public sealed class MaintenanceRecordTests
{
    private static readonly Guid TenantId  = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();

    [Fact]
    public void CreateScheduled_sets_correct_properties()
    {
        var before = DateTime.UtcNow;
        var record = MaintenanceRecord.CreateScheduled(
            TenantId, VehicleId, "ODOMETER", odometerKm: 10_001m, thresholdKm: 10_000m, notes: "test note");

        record.Id.Should().NotBeEmpty();
        record.TenantId.Should().Be(TenantId);
        record.VehicleId.Should().Be(VehicleId);
        record.RecordType.Should().Be("SCHEDULED");
        record.TriggeredBy.Should().Be("ODOMETER");
        record.OdometerKm.Should().Be(10_001m);
        record.ThresholdKm.Should().Be(10_000m);
        record.Notes.Should().Be("test note");
        record.ResolvedAt.Should().BeNull();
        record.CreatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void CreateCriticalAlert_sets_correct_properties()
    {
        var record = MaintenanceRecord.CreateCriticalAlert(
            TenantId, VehicleId, "P0300", odometerKm: 5_000m);

        record.RecordType.Should().Be("CRITICAL_ALERT");
        record.TriggeredBy.Should().Be("OBD2_CODE");
        record.Obd2Code.Should().Be("P0300");
        record.OdometerKm.Should().Be(5_000m);
        record.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public void Resolve_sets_resolved_at_and_changes_type_to_completed()
    {
        var record = MaintenanceRecord.CreateScheduled(TenantId, VehicleId, "ODOMETER");
        var before = DateTime.UtcNow;

        record.Resolve();

        record.RecordType.Should().Be("COMPLETED");
        record.ResolvedAt.Should().NotBeNull();
        record.ResolvedAt!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Each_record_gets_unique_id()
    {
        var a = MaintenanceRecord.CreateScheduled(TenantId, VehicleId, "X");
        var b = MaintenanceRecord.CreateScheduled(TenantId, VehicleId, "X");

        a.Id.Should().NotBe(b.Id);
    }
}
