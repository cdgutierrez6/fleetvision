using FleetVision.PredictiveMaintenance.Domain.Entities;
using FleetVision.PredictiveMaintenance.Domain.ValueObjects;

namespace FleetVision.PredictiveMaintenance.Domain.Rules;

public sealed class OBD2WarningRule : IMaintenanceRule
{
    public bool IsSatisfiedBy(
        TelemetryReading reading,
        OdometerSnapshot odometer,
        MaintenanceThresholds thresholds,
        DateTime? lastMaintenanceAt)
    {
        var code = OBD2Code.TryParse(reading.Obd2Code);
        return code is not null && code.Severity == OBD2Severity.Warning;
    }

    public MaintenanceRecord CreateRecord(
        TelemetryReading reading,
        OdometerSnapshot odometer,
        MaintenanceThresholds thresholds)
        => MaintenanceRecord.CreateScheduled(
            tenantId:    reading.TenantId,
            vehicleId:   reading.VehicleId,
            triggeredBy: "OBD2_CODE",
            odometerKm:  odometer.IsUnknown ? null : odometer.Km,
            notes:       $"Código OBD2: {reading.Obd2Code} (advertencia)");
}
