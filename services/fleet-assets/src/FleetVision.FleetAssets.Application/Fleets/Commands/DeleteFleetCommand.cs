using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.FleetAssets.Application.Fleets.Commands;

public sealed record DeleteFleetCommand(Guid Id, Guid TenantId) : IRequest;

public sealed class DeleteFleetCommandHandler : IRequestHandler<DeleteFleetCommand>
{
    private readonly IFleetAssetsDbContext _db;

    public DeleteFleetCommandHandler(IFleetAssetsDbContext db) => _db = db;

    public async Task Handle(DeleteFleetCommand command, CancellationToken ct)
    {
        var fleet = await _db.Fleets
            .FirstOrDefaultAsync(f => f.Id == command.Id && f.TenantId == command.TenantId, ct)
            ?? throw new FleetNotFoundException(command.Id);

        _db.Fleets.Remove(fleet);
        await _db.SaveChangesAsync(ct);
    }
}
