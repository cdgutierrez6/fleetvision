using FleetVision.PredictiveMaintenance.Domain.Entities;

namespace FleetVision.PredictiveMaintenance.Domain.Interfaces;

public interface IMaintenanceOutboxEnqueuer
{
    void EnqueueScheduled(MaintenanceRecord record);
    void EnqueueAlert(MaintenanceRecord record);
}
