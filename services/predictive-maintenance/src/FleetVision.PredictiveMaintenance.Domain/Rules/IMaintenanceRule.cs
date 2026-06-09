using FleetVision.PredictiveMaintenance.Domain.Entities;
using FleetVision.PredictiveMaintenance.Domain.ValueObjects;

namespace FleetVision.PredictiveMaintenance.Domain.Rules;

public interface IMaintenanceRule
{
    bool IsSatisfiedBy(
        TelemetryReading reading,
        OdometerSnapshot odometer,
        MaintenanceThresholds thresholds,
        DateTime? lastMaintenanceAt);

    MaintenanceRecord CreateRecord(
        TelemetryReading reading,
        OdometerSnapshot odometer,
        MaintenanceThresholds thresholds);
}
