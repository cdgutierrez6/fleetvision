using FleetVision.PredictiveMaintenance.Domain.Entities;
using FleetVision.PredictiveMaintenance.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.PredictiveMaintenance.Infrastructure.Persistence;

public sealed class MaintenanceRepository : IMaintenanceRepository
{
    private readonly MaintenanceDbContext _db;

    public MaintenanceRepository(MaintenanceDbContext db) => _db = db;

    public async Task AddAsync(MaintenanceRecord record, CancellationToken ct = default)
        => await _db.Records.AddAsync(record, ct);

    public async Task<MaintenanceRecord?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        => await _db.Records.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<MaintenanceRecord>> GetByVehicleAsync(
        Guid vehicleId, Guid tenantId, int page, int pageSize, CancellationToken ct = default)
        => await _db.Records
            .Where(r => r.VehicleId == vehicleId && r.TenantId == tenantId)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public async Task<DateTime?> GetLastMaintenanceAtAsync(
        Guid vehicleId, Guid tenantId, CancellationToken ct = default)
        => await _db.Records
            .Where(r => r.VehicleId == vehicleId && r.TenantId == tenantId && r.ResolvedAt != null)
            .MaxAsync(r => (DateTime?)r.ResolvedAt, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
