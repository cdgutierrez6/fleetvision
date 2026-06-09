using FleetVision.PredictiveMaintenance.Domain.Entities;
using FleetVision.PredictiveMaintenance.Domain.Interfaces;
using MediatR;

namespace FleetVision.PredictiveMaintenance.Application.Queries;

public sealed record GetMaintenanceRecordsQuery(
    Guid VehicleId, Guid TenantId, int Page = 1, int PageSize = 20)
    : IRequest<IReadOnlyList<MaintenanceRecord>>;

public sealed class GetMaintenanceRecordsHandler
    : IRequestHandler<GetMaintenanceRecordsQuery, IReadOnlyList<MaintenanceRecord>>
{
    private readonly IMaintenanceRepository _repository;

    public GetMaintenanceRecordsHandler(IMaintenanceRepository repository)
        => _repository = repository;

    public Task<IReadOnlyList<MaintenanceRecord>> Handle(
        GetMaintenanceRecordsQuery query, CancellationToken ct)
        => _repository.GetByVehicleAsync(
            query.VehicleId, query.TenantId, query.Page, query.PageSize, ct);
}
