using FleetVision.PredictiveMaintenance.Domain.Entities;

namespace FleetVision.PredictiveMaintenance.Domain.Interfaces;

public interface IMaintenanceRepository
{
    Task AddAsync(MaintenanceRecord record, CancellationToken ct = default);
    Task<MaintenanceRecord?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<MaintenanceRecord>> GetByVehicleAsync(Guid vehicleId, Guid tenantId, int page, int pageSize, CancellationToken ct = default);
    Task<DateTime?> GetLastMaintenanceAtAsync(Guid vehicleId, Guid tenantId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
