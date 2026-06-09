using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.FleetAssets.Application.VehicleAssignments.Commands;

public sealed record CloseVehicleAssignmentCommand(Guid TenantId, Guid VehicleId) : IRequest;

public sealed class CloseVehicleAssignmentCommandHandler : IRequestHandler<CloseVehicleAssignmentCommand>
{
    private readonly IFleetAssetsDbContext _db;

    public CloseVehicleAssignmentCommandHandler(IFleetAssetsDbContext db) => _db = db;

    public async Task Handle(CloseVehicleAssignmentCommand command, CancellationToken ct)
    {
        var assignment = await _db.VehicleAssignments
            .FirstOrDefaultAsync(
                a => a.VehicleId == command.VehicleId
                  && a.TenantId == command.TenantId
                  && a.EndedAt == null, ct)
            ?? throw new AssignmentNotFoundException(command.VehicleId);

        assignment.Close();
        await _db.SaveChangesAsync(ct);
    }
}
