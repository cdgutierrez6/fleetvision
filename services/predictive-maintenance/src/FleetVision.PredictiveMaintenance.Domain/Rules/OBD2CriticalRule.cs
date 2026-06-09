using FleetVision.PredictiveMaintenance.Domain.Entities;
using FleetVision.PredictiveMaintenance.Domain.ValueObjects;

namespace FleetVision.PredictiveMaintenance.Domain.Rules;

public sealed class OBD2CriticalRule : IMaintenanceRule
{
    public bool IsSatisfiedBy(
        TelemetryReading reading,
        OdometerSnapshot odometer,
        MaintenanceThresholds thresholds,
        DateTime? lastMaintenanceAt)
    {
        var code = OBD2Code.TryParse(reading.Obd2Code);
        return code is not null && code.IsCritical;
    }

    public MaintenanceRecord CreateRecord(
        TelemetryReading reading,
        OdometerSnapshot odometer,
        MaintenanceThresholds thresholds)
        => MaintenanceRecord.CreateCriticalAlert(
            tenantId:   reading.TenantId,
            vehicleId:  reading.VehicleId,
            obd2Code:   reading.Obd2Code!,
            odometerKm: odometer.IsUnknown ? null : odometer.Km);
}
