using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.FleetAssets.Application.Drivers.Commands;

public sealed record DeleteDriverCommand(Guid Id, Guid TenantId) : IRequest;

public sealed class DeleteDriverCommandHandler : IRequestHandler<DeleteDriverCommand>
{
    private readonly IFleetAssetsDbContext _db;

    public DeleteDriverCommandHandler(IFleetAssetsDbContext db) => _db = db;

    public async Task Handle(DeleteDriverCommand command, CancellationToken ct)
    {
        var driver = await _db.Drivers
            .FirstOrDefaultAsync(d => d.Id == command.Id && d.TenantId == command.TenantId, ct)
            ?? throw new DriverNotFoundException(command.Id);

        driver.SoftDelete();
        await _db.SaveChangesAsync(ct);
    }
}
