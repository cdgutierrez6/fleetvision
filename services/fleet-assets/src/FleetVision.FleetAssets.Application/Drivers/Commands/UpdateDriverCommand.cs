using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Application.DTOs;
using FleetVision.FleetAssets.Domain.Enums;
using FleetVision.FleetAssets.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.FleetAssets.Application.Drivers.Commands;

public sealed record UpdateDriverCommand(
    Guid Id, Guid TenantId, string FullName, string LicenseNumber,
    string? Phone, string? Email, DriverStatus Status)
    : IRequest<DriverDto>;

public sealed class UpdateDriverCommandHandler : IRequestHandler<UpdateDriverCommand, DriverDto>
{
    private readonly IFleetAssetsDbContext _db;

    public UpdateDriverCommandHandler(IFleetAssetsDbContext db) => _db = db;

    public async Task<DriverDto> Handle(UpdateDriverCommand command, CancellationToken ct)
    {
        var driver = await _db.Drivers
            .FirstOrDefaultAsync(d => d.Id == command.Id && d.TenantId == command.TenantId, ct)
            ?? throw new DriverNotFoundException(command.Id);

        driver.Update(command.FullName, command.LicenseNumber,
            command.Phone, command.Email, command.Status);
        await _db.SaveChangesAsync(ct);
        return FleetAssetsMappings.ToDto(driver);
    }
}

public sealed class UpdateDriverCommandValidator : AbstractValidator<UpdateDriverCommand>
{
    public UpdateDriverCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LicenseNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Phone).MaximumLength(20).When(x => x.Phone is not null);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(150).When(x => x.Email is not null);
        RuleFor(x => x.Status).IsInEnum();
    }
}
