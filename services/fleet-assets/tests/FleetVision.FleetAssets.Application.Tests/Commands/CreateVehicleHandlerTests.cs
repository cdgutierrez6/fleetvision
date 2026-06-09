using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Application.Fleets.Commands;
using FleetVision.FleetAssets.Application.Vehicles.Commands;
using FleetVision.FleetAssets.Domain.Exceptions;
using FleetVision.FleetAssets.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace FleetVision.FleetAssets.Application.Tests.Commands;

public sealed class CreateVehicleHandlerTests : IDisposable
{
    private readonly FleetAssetsDbContext _db;
    private readonly ITenantLimitsClient _limitsClient;
    private readonly CreateVehicleCommandHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private Guid _fleetId;

    public CreateVehicleHandlerTests()
    {
        var options = new DbContextOptionsBuilder<FleetAssetsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db           = new FleetAssetsDbContext(options);
        _limitsClient = Substitute.For<ITenantLimitsClient>();
        _handler      = new CreateVehicleCommandHandler(_db, _limitsClient);
    }

    private async Task<Guid> SeedFleet()
    {
        var fleetHandler = new CreateFleetCommandHandler(_db);
        var fleet = await fleetHandler.Handle(
            new CreateFleetCommand(_tenantId, "Test Fleet", null), default);
        _fleetId = fleet.Id;
        return fleet.Id;
    }

    private void SetupLimits(int maxVehicles = 25, string plan = "Starter")
    {
        _limitsClient
            .GetLimitsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new TenantLimitsResponse(_tenantId, plan, maxVehicles, 25, true));
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnVehicleDto()
    {
        await SeedFleet();
        SetupLimits();

        var command = new CreateVehicleCommand(_tenantId, _fleetId, "ABC-123", null, "Toyota", "Hilux", 2022);
        var result  = await _handler.Handle(command, default);

        result.Id.Should().NotBeEmpty();
        result.TenantId.Should().Be(_tenantId);
        result.Plate.Should().Be("ABC-123");
        result.Status.Should().Be("Active");
        result.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithFleetNotFound_ShouldThrow()
    {
        SetupLimits();

        var command = new CreateVehicleCommand(_tenantId, Guid.NewGuid(), "ABC", null, "Brand", "Model", 2020);
        var act     = async () => await _handler.Handle(command, default);

        await act.Should().ThrowAsync<FleetNotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenAtPlanLimit_ShouldThrowVehiclePlanLimitExceededException()
    {
        await SeedFleet();

        // Seed 3 vehicles (Free plan limit)
        SetupLimits(maxVehicles: 3, plan: "Free");
        for (var i = 0; i < 3; i++)
        {
            await _handler.Handle(
                new CreateVehicleCommand(_tenantId, _fleetId, $"PL-00{i}", null, "Brand", "Model", 2020), default);
        }

        // 4th vehicle should be rejected
        var act = async () => await _handler.Handle(
            new CreateVehicleCommand(_tenantId, _fleetId, "PL-004", null, "Brand", "Model", 2020), default);

        await act.Should().ThrowAsync<VehiclePlanLimitExceededException>()
            .WithMessage("*Free*3*");
    }

    [Fact]
    public async Task Handle_ShouldCallLimitsClientOnce()
    {
        await SeedFleet();
        SetupLimits();

        await _handler.Handle(
            new CreateVehicleCommand(_tenantId, _fleetId, "ABC", null, "Brand", "Model", 2020), default);

        await _limitsClient.Received(1).GetLimitsAsync(_tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FleetFromDifferentTenant_ShouldThrowFleetNotFoundException()
    {
        var otherTenantId = Guid.NewGuid();
        var otherFleetHandler = new CreateFleetCommandHandler(_db);
        var otherFleet = await otherFleetHandler.Handle(
            new CreateFleetCommand(otherTenantId, "Other Fleet", null), default);

        SetupLimits();

        // Try to create vehicle for _tenantId but with fleet from otherTenantId
        var act = async () => await _handler.Handle(
            new CreateVehicleCommand(_tenantId, otherFleet.Id, "ABC", null, "Brand", "Model", 2020), default);

        await act.Should().ThrowAsync<FleetNotFoundException>();
    }

    public void Dispose() => _db.Dispose();
}
