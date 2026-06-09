using FleetVision.PredictiveMaintenance.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FleetVision.PredictiveMaintenance.Application.Commands;

public sealed record CompleteMaintenanceCommand(Guid RecordId, Guid TenantId) : IRequest<bool>;

public sealed class CompleteMaintenanceHandler : IRequestHandler<CompleteMaintenanceCommand, bool>
{
    private readonly IMaintenanceRepository          _repository;
    private readonly IOdometerCache                  _odometerCache;
    private readonly ILogger<CompleteMaintenanceHandler> _logger;

    public CompleteMaintenanceHandler(
        IMaintenanceRepository repository,
        IOdometerCache odometerCache,
        ILogger<CompleteMaintenanceHandler> logger)
    {
        _repository    = repository;
        _odometerCache = odometerCache;
        _logger        = logger;
    }

    public async Task<bool> Handle(CompleteMaintenanceCommand cmd, CancellationToken ct)
    {
        var record = await _repository.GetByIdAsync(cmd.RecordId, cmd.TenantId, ct);
        if (record is null)
        {
            _logger.LogWarning("CompleteMaintenanceCommand: record {RecordId} not found for tenant {TenantId}",
                cmd.RecordId, cmd.TenantId);
            return false;
        }

        record.Resolve();
        await _odometerCache.ResetAsync(cmd.TenantId, record.VehicleId, ct);
        await _repository.SaveChangesAsync(ct);

        _logger.LogInformation(
            "AUDIT maintenance_completed RecordId={RecordId} VehicleId={VehicleId} TenantId={TenantId}",
            record.Id, record.VehicleId, cmd.TenantId);

        return true;
    }
}
