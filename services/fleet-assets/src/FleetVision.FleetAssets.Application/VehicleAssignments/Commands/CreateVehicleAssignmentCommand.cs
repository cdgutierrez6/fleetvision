using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Application.DTOs;
using FleetVision.FleetAssets.Domain.Entities;
using FleetVision.FleetAssets.Domain.Enums;
using FleetVision.FleetAssets.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.FleetAssets.Application.VehicleAssignments.Commands;

public sealed record CreateVehicleAssignmentCommand(Guid TenantId, Guid VehicleId, Guid DriverId)
    : IRequest<AssignmentDto>;

public sealed class CreateVehicleAssignmentCommandHandler
    : IRequestHandler<CreateVehicleAssignmentCommand, AssignmentDto>
{
    private readonly IFleetAssetsDbContext _db;

    public CreateVehicleAssignmentCommandHandler(IFleetAssetsDbContext db) => _db = db;

    public async Task<AssignmentDto> Handle(CreateVehicleAssignmentCommand command, CancellationToken ct)
    {
        var vehicleExists = await _db.Vehicles
            .AnyAsync(v => v.Id == command.VehicleId && v.TenantId == command.TenantId, ct);
        if (!vehicleExists) throw new VehicleNotFoundException(command.VehicleId);

        var driver = await _db.Drivers
            .FirstOrDefaultAsync(d => d.Id == command.DriverId && d.TenantId == command.TenantId, ct)
            ?? throw new DriverNotFoundException(command.DriverId);

        if (driver.Status != DriverStatus.Active)
            throw new DriverInactiveException(command.DriverId);

        var hasActive = await _db.VehicleAssignments
            .AnyAsync(a => a.VehicleId == command.VehicleId && a.EndedAt == null, ct);
        if (hasActive) throw new ActiveAssignmentExistsException(command.VehicleId);

        var assignment = VehicleAssignment.Create(command.TenantId, command.VehicleId, command.DriverId);
        _db.VehicleAssignments.Add(assignment);
        await _db.SaveChangesAsync(ct);
        return FleetAssetsMappings.ToDto(assignment);
    }
}

public sealed class CreateVehicleAssignmentCommandValidator : AbstractValidator<CreateVehicleAssignmentCommand>
{
    public CreateVehicleAssignmentCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.DriverId).NotEmpty();
    }
}
