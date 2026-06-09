using FleetVision.FleetAssets.Application.Fleets.Commands;
using FleetVision.FleetAssets.Domain.Exceptions;
using FleetVision.FleetAssets.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FleetVision.FleetAssets.Application.Tests.Commands;

public sealed class CreateFleetHandlerTests : IDisposable
{
    private readonly FleetAssetsDbContext _db;
    private readonly CreateFleetCommandHandler _handler;

    public CreateFleetHandlerTests()
    {
        var options = new DbContextOptionsBuilder<FleetAssetsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db      = new FleetAssetsDbContext(options);
        _handler = new CreateFleetCommandHandler(_db);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnFleetDto()
    {
        var tenantId = Guid.NewGuid();
        var command  = new CreateFleetCommand(tenantId, "North Fleet", "Northern region");

        var result = await _handler.Handle(command, default);

        result.Id.Should().NotBeEmpty();
        result.TenantId.Should().Be(tenantId);
        result.Name.Should().Be("North Fleet");
        result.Description.Should().Be("Northern region");
    }

    [Fact]
    public async Task Handle_ShouldPersistFleet()
    {
        var tenantId = Guid.NewGuid();
        var command  = new CreateFleetCommand(tenantId, "Fleet A", null);

        await _handler.Handle(command, default);

        var saved = await _db.Fleets.FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.TenantId.Should().Be(tenantId);
        saved.Name.Should().Be("Fleet A");
    }

    [Fact]
    public async Task Handle_FleetScopedToTenant_ShouldNotVisibleToOtherTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await _handler.Handle(new CreateFleetCommand(tenantA, "Fleet A", null), default);
        await _handler.Handle(new CreateFleetCommand(tenantB, "Fleet B", null), default);

        var fleetsByA = await _db.Fleets.Where(f => f.TenantId == tenantA).ToListAsync();
        fleetsByA.Should().HaveCount(1);
        fleetsByA[0].Name.Should().Be("Fleet A");
    }

    [Fact]
    public async Task Handle_NullDescription_ShouldCreateFleetWithoutDescription()
    {
        var result = await _handler.Handle(
            new CreateFleetCommand(Guid.NewGuid(), "Fleet", null), default);

        result.Description.Should().BeNull();
    }

    public void Dispose() => _db.Dispose();
}
