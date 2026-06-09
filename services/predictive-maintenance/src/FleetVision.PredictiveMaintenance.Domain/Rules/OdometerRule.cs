using FleetVision.PredictiveMaintenance.Domain.Entities;
using FleetVision.PredictiveMaintenance.Domain.ValueObjects;

namespace FleetVision.PredictiveMaintenance.Domain.Rules;

public sealed class OdometerRule : IMaintenanceRule
{
    public bool IsSatisfiedBy(
        TelemetryReading reading,
        OdometerSnapshot odometer,
        MaintenanceThresholds thresholds,
        DateTime? lastMaintenanceAt)
        => !odometer.IsUnknown && odometer.Km >= thresholds.OdometerKm;

    public MaintenanceRecord CreateRecord(
        TelemetryReading reading,
        OdometerSnapshot odometer,
        MaintenanceThresholds thresholds)
        => MaintenanceRecord.CreateScheduled(
            tenantId:    reading.TenantId,
            vehicleId:   reading.VehicleId,
            triggeredBy: "ODOMETER",
            odometerKm:  odometer.Km,
            thresholdKm: thresholds.OdometerKm,
            notes:       $"Acumulado {odometer.Km:F0} km — umbral {thresholds.OdometerKm:F0} km");
}
