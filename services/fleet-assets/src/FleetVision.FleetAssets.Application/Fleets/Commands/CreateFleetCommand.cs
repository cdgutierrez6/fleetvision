using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Application.DTOs;
using FleetVision.FleetAssets.Domain.Entities;
using FluentValidation;
using MediatR;

namespace FleetVision.FleetAssets.Application.Fleets.Commands;

public sealed record CreateFleetCommand(Guid TenantId, string Name, string? Description)
    : IRequest<FleetDto>;

public sealed class CreateFleetCommandHandler : IRequestHandler<CreateFleetCommand, FleetDto>
{
    private readonly IFleetAssetsDbContext _db;

    public CreateFleetCommandHandler(IFleetAssetsDbContext db) => _db = db;

    public async Task<FleetDto> Handle(CreateFleetCommand command, CancellationToken ct)
    {
        var fleet = Fleet.Create(command.TenantId, command.Name, command.Description);
        _db.Fleets.Add(fleet);
        await _db.SaveChangesAsync(ct);
        return FleetAssetsMappings.ToDto(fleet);
    }
}

public sealed class CreateFleetCommandValidator : AbstractValidator<CreateFleetCommand>
{
    public CreateFleetCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500).When(x => x.Description is not null);
    }
}
