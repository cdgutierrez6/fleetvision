using FleetVision.PredictiveMaintenance.Domain.Entities;
using FleetVision.PredictiveMaintenance.Domain.ValueObjects;

namespace FleetVision.PredictiveMaintenance.Domain.Rules;

public sealed class TimeBasedRule : IMaintenanceRule
{
    public bool IsSatisfiedBy(
        TelemetryReading reading,
        OdometerSnapshot odometer,
        MaintenanceThresholds thresholds,
        DateTime? lastMaintenanceAt)
    {
        if (lastMaintenanceAt is null) return false;
        return (reading.Timestamp - lastMaintenanceAt.Value).TotalDays >= thresholds.TimeBasedDays;
    }

    public MaintenanceRecord CreateRecord(
        TelemetryReading reading,
        OdometerSnapshot odometer,
        MaintenanceThresholds thresholds)
        => MaintenanceRecord.CreateScheduled(
            tenantId:    reading.TenantId,
            vehicleId:   reading.VehicleId,
            triggeredBy: "TIME_BASED",
            odometerKm:  odometer.IsUnknown ? null : odometer.Km,
            notes:       $"Mantenimiento periódico: umbral {thresholds.TimeBasedDays} días alcanzado");
}
