using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Application.DTOs;
using FleetVision.FleetAssets.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.FleetAssets.Application.Fleets.Commands;

public sealed record UpdateFleetCommand(Guid Id, Guid TenantId, string Name, string? Description)
    : IRequest<FleetDto>;

public sealed class UpdateFleetCommandHandler : IRequestHandler<UpdateFleetCommand, FleetDto>
{
    private readonly IFleetAssetsDbContext _db;

    public UpdateFleetCommandHandler(IFleetAssetsDbContext db) => _db = db;

    public async Task<FleetDto> Handle(UpdateFleetCommand command, CancellationToken ct)
    {
        var fleet = await _db.Fleets
            .FirstOrDefaultAsync(f => f.Id == command.Id && f.TenantId == command.TenantId, ct)
            ?? throw new FleetNotFoundException(command.Id);

        fleet.Update(command.Name, command.Description);
        await _db.SaveChangesAsync(ct);
        return FleetAssetsMappings.ToDto(fleet);
    }
}

public sealed class UpdateFleetCommandValidator : AbstractValidator<UpdateFleetCommand>
{
    public UpdateFleetCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
    }
}
