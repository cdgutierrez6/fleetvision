using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Application.Drivers.Commands;
using FleetVision.FleetAssets.Application.Fleets.Commands;
using FleetVision.FleetAssets.Application.VehicleAssignments.Commands;
using FleetVision.FleetAssets.Application.Vehicles.Commands;
using FleetVision.FleetAssets.Domain.Exceptions;
using FleetVision.FleetAssets.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace FleetVision.FleetAssets.Application.Tests.Commands;

public sealed class CreateVehicleAssignmentHandlerTests : IDisposable
{
    private readonly FleetAssetsDbContext _db;
    private readonly CreateVehicleAssignmentCommandHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public CreateVehicleAssignmentHandlerTests()
    {
        var options = new DbContextOptionsBuilder<FleetAssetsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db      = new FleetAssetsDbContext(options);
        _handler = new CreateVehicleAssignmentCommandHandler(_db);
    }

    private async Task<Guid> SeedVehicleAsync()
    {
        var fleetHandler = new CreateFleetCommandHandler(_db);
        var fleet = await fleetHandler.Handle(
            new CreateFleetCommand(_tenantId, "Fleet", null), default);

        var limitsClient = Substitute.For<ITenantLimitsClient>();
        limitsClient.GetLimitsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new TenantLimitsResponse(_tenantId, "Starter", 25, 25, true));

        var vehicleHandler = new CreateVehicleCommandHandler(_db, limitsClient);
        var vehicle = await vehicleHandler.Handle(
            new CreateVehicleCommand(_tenantId, fleet.Id, "ABC-123", null, "Brand", "Model", 2020), default);

        return vehicle.Id;
    }

    private async Task<Guid> SeedDriverAsync()
    {
        var handler = new CreateDriverCommandHandler(_db);
        var driver  = await handler.Handle(
            new CreateDriverCommand(_tenantId, "Juan Pérez", "LIC-001"), default);
        return driver.Id;
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldCreateAssignment()
    {
        var vehicleId = await SeedVehicleAsync();
        var driverId  = await SeedDriverAsync();

        var result = await _handler.Handle(
            new CreateVehicleAssignmentCommand(_tenantId, vehicleId, driverId), default);

        result.VehicleId.Should().Be(vehicleId);
        result.DriverId.Should().Be(driverId);
        result.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        result.EndedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenVehicleAlreadyAssigned_ShouldThrowActiveAssignmentExistsException()
    {
        var vehicleId = await SeedVehicleAsync();
        var driver1   = await SeedDriverAsync();
        var driver2   = await SeedDriverAsync();

        await _handler.Handle(new CreateVehicleAssignmentCommand(_tenantId, vehicleId, driver1), default);

        var act = async () => await _handler.Handle(
            new CreateVehicleAssignmentCommand(_tenantId, vehicleId, driver2), default);

        await act.Should().ThrowAsync<ActiveAssignmentExistsException>();
    }

    [Fact]
    public async Task Handle_WithNonExistentVehicle_ShouldThrowVehicleNotFoundException()
    {
        var driverId = await SeedDriverAsync();

        var act = async () => await _handler.Handle(
            new CreateVehicleAssignmentCommand(_tenantId, Guid.NewGuid(), driverId), default);

        await act.Should().ThrowAsync<VehicleNotFoundException>();
    }

    [Fact]
    public async Task Handle_WithNonExistentDriver_ShouldThrowDriverNotFoundException()
    {
        var vehicleId = await SeedVehicleAsync();

        var act = async () => await _handler.Handle(
            new CreateVehicleAssignmentCommand(_tenantId, vehicleId, Guid.NewGuid()), default);

        await act.Should().ThrowAsync<DriverNotFoundException>();
    }

    [Fact]
    public async Task Handle_CanReopenAfterClose_ShouldSucceed()
    {
        var vehicleId = await SeedVehicleAsync();
        var driverId  = await SeedDriverAsync();

        await _handler.Handle(new CreateVehicleAssignmentCommand(_tenantId, vehicleId, driverId), default);

        var closeHandler = new CloseVehicleAssignmentCommandHandler(_db);
        await closeHandler.Handle(new CloseVehicleAssignmentCommand(_tenantId, vehicleId), default);

        // Second assignment after closing — should succeed
        var result = await _handler.Handle(
            new CreateVehicleAssignmentCommand(_tenantId, vehicleId, driverId), default);

        result.EndedAt.Should().BeNull();
    }

    [Fact]
    public async Task CloseAssignment_WithNoActive_ShouldThrowAssignmentNotFoundException()
    {
        var vehicleId = await SeedVehicleAsync();

        var closeHandler = new CloseVehicleAssignmentCommandHandler(_db);
        var act = async () => await closeHandler.Handle(
            new CloseVehicleAssignmentCommand(_tenantId, vehicleId), default);

        await act.Should().ThrowAsync<AssignmentNotFoundException>();
    }

    public void Dispose() => _db.Dispose();
}
