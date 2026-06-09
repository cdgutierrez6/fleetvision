using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Application.DTOs;
using FleetVision.FleetAssets.Domain.Entities;
using FluentValidation;
using MediatR;

namespace FleetVision.FleetAssets.Application.Drivers.Commands;

public sealed record CreateDriverCommand(
    Guid TenantId, string FullName, string LicenseNumber,
    string? Phone = null, string? Email = null)
    : IRequest<DriverDto>;

public sealed class CreateDriverCommandHandler : IRequestHandler<CreateDriverCommand, DriverDto>
{
    private readonly IFleetAssetsDbContext _db;

    public CreateDriverCommandHandler(IFleetAssetsDbContext db) => _db = db;

    public async Task<DriverDto> Handle(CreateDriverCommand command, CancellationToken ct)
    {
        var driver = Driver.Create(
            command.TenantId, command.FullName, command.LicenseNumber,
            command.Phone, command.Email);

        _db.Drivers.Add(driver);
        await _db.SaveChangesAsync(ct);
        return FleetAssetsMappings.ToDto(driver);
    }
}

public sealed class CreateDriverCommandValidator : AbstractValidator<CreateDriverCommand>
{
    public CreateDriverCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LicenseNumber).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Phone).MaximumLength(20).When(x => x.Phone is not null);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(150).When(x => x.Email is not null);
    }
}
