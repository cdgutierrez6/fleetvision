using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.FleetAssets.Application.Vehicles.Commands;

public sealed record DeleteVehicleCommand(Guid Id, Guid TenantId) : IRequest;

public sealed class DeleteVehicleCommandHandler : IRequestHandler<DeleteVehicleCommand>
{
    private readonly IFleetAssetsDbContext _db;

    public DeleteVehicleCommandHandler(IFleetAssetsDbContext db) => _db = db;

    public async Task Handle(DeleteVehicleCommand command, CancellationToken ct)
    {
        // Global query filter excludes already-deleted vehicles, so not-found = 404 either way
        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == command.Id && v.TenantId == command.TenantId, ct)
            ?? throw new VehicleNotFoundException(command.Id);

        vehicle.SoftDelete();
        await _db.SaveChangesAsync(ct);
    }
}
