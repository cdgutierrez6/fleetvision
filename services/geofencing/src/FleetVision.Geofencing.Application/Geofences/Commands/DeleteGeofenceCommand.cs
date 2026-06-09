using FleetVision.Geofencing.Application.Common;
using FleetVision.Geofencing.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.Geofencing.Application.Geofences.Commands;

public sealed record DeleteGeofenceCommand(Guid Id, Guid TenantId) : IRequest;

public sealed class DeleteGeofenceCommandHandler : IRequestHandler<DeleteGeofenceCommand>
{
    private readonly IGeofencingDbContext _db;

    public DeleteGeofenceCommandHandler(IGeofencingDbContext db) => _db = db;

    public async Task Handle(DeleteGeofenceCommand command, CancellationToken ct)
    {
        var geofence = await _db.Geofences
            .FirstOrDefaultAsync(g => g.Id == command.Id && g.TenantId == command.TenantId, ct)
            ?? throw new GeofenceNotFoundException(command.Id);

        _db.Geofences.Remove(geofence);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class DeleteGeofenceCommandValidator : AbstractValidator<DeleteGeofenceCommand>
{
    public DeleteGeofenceCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
    }
}
