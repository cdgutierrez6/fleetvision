using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.FleetAssets.Application.Vehicles.Commands;

public sealed record UpdateVehiclePositionCommand(Guid Id, Guid TenantId, double Longitude, double Latitude)
    : IRequest;

public sealed class UpdateVehiclePositionCommandHandler : IRequestHandler<UpdateVehiclePositionCommand>
{
    private readonly IFleetAssetsDbContext _db;

    public UpdateVehiclePositionCommandHandler(IFleetAssetsDbContext db) => _db = db;

    public async Task Handle(UpdateVehiclePositionCommand command, CancellationToken ct)
    {
        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == command.Id && v.TenantId == command.TenantId, ct)
            ?? throw new VehicleNotFoundException(command.Id);

        vehicle.UpdatePosition(command.Longitude, command.Latitude);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class UpdateVehiclePositionCommandValidator : AbstractValidator<UpdateVehiclePositionCommand>
{
    public UpdateVehiclePositionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
    }
}
