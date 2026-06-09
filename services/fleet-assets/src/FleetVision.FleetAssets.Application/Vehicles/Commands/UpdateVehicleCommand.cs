using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Application.DTOs;
using FleetVision.FleetAssets.Domain.Enums;
using FleetVision.FleetAssets.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.FleetAssets.Application.Vehicles.Commands;

public sealed record UpdateVehicleCommand(
    Guid Id, Guid TenantId, string Plate, string Brand,
    string Model, int Year, int OdometerKm, VehicleStatus Status)
    : IRequest<VehicleDto>;

public sealed class UpdateVehicleCommandHandler : IRequestHandler<UpdateVehicleCommand, VehicleDto>
{
    private readonly IFleetAssetsDbContext _db;

    public UpdateVehicleCommandHandler(IFleetAssetsDbContext db) => _db = db;

    public async Task<VehicleDto> Handle(UpdateVehicleCommand command, CancellationToken ct)
    {
        var vehicle = await _db.Vehicles
            .FirstOrDefaultAsync(v => v.Id == command.Id && v.TenantId == command.TenantId, ct)
            ?? throw new VehicleNotFoundException(command.Id);

        vehicle.Update(command.Plate, command.Brand, command.Model,
            command.Year, command.OdometerKm, command.Status);
        await _db.SaveChangesAsync(ct);
        return FleetAssetsMappings.ToDto(vehicle);
    }
}

public sealed class UpdateVehicleCommandValidator : AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Plate).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Brand).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Model).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Year).GreaterThanOrEqualTo(1900).LessThanOrEqualTo(DateTime.UtcNow.Year + 1);
        RuleFor(x => x.OdometerKm).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Status).IsInEnum();
    }
}
