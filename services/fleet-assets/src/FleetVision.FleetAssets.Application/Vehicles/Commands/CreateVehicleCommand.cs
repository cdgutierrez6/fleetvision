using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Application.DTOs;
using FleetVision.FleetAssets.Domain.Entities;
using FleetVision.FleetAssets.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.FleetAssets.Application.Vehicles.Commands;

public sealed record CreateVehicleCommand(
    Guid TenantId, Guid FleetId, string Plate, string? Vin,
    string Brand, string Model, int Year, int OdometerKm = 0)
    : IRequest<VehicleDto>;

public sealed class CreateVehicleCommandHandler : IRequestHandler<CreateVehicleCommand, VehicleDto>
{
    private readonly IFleetAssetsDbContext _db;
    private readonly ITenantLimitsClient _limitsClient;

    public CreateVehicleCommandHandler(IFleetAssetsDbContext db, ITenantLimitsClient limitsClient)
    {
        _db           = db;
        _limitsClient = limitsClient;
    }

    public async Task<VehicleDto> Handle(CreateVehicleCommand command, CancellationToken ct)
    {
        var fleetExists = await _db.Fleets
            .AnyAsync(f => f.Id == command.FleetId && f.TenantId == command.TenantId, ct);
        if (!fleetExists) throw new FleetNotFoundException(command.FleetId);

        var limits = await _limitsClient.GetLimitsAsync(command.TenantId, ct);

        var currentCount = await _db.Vehicles
            .Where(v => v.TenantId == command.TenantId)
            .CountAsync(ct);

        if (currentCount >= limits.MaxVehicles)
            throw new VehiclePlanLimitExceededException(limits.MaxVehicles, limits.Plan);

        var vehicle = Vehicle.Create(
            command.TenantId, command.FleetId, command.Plate, command.Vin,
            command.Brand, command.Model, command.Year, command.OdometerKm);

        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync(ct);
        return FleetAssetsMappings.ToDto(vehicle);
    }
}

public sealed class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.FleetId).NotEmpty();
        RuleFor(x => x.Plate).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Vin).MaximumLength(17).When(x => x.Vin is not null);
        RuleFor(x => x.Brand).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Model).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Year).GreaterThanOrEqualTo(1900).LessThanOrEqualTo(DateTime.UtcNow.Year + 1);
        RuleFor(x => x.OdometerKm).GreaterThanOrEqualTo(0);
    }
}
