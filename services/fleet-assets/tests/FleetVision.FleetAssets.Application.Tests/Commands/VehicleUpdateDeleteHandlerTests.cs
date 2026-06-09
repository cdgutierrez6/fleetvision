using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Application.Fleets.Commands;
using FleetVision.FleetAssets.Application.Vehicles.Commands;
using FleetVision.FleetAssets.Application.Vehicles.Queries;
using FleetVision.FleetAssets.Domain.Enums;
using FleetVision.FleetAssets.Domain.Exceptions;
using FleetVision.FleetAssets.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace FleetVision.FleetAssets.Application.Tests.Commands;

public sealed class VehicleUpdateDeleteHandlerTests : IDisposable
{
    private readonly FleetAssetsDbContext _db;
    private readonly UpdateVehicleCommandHandler _updateHandler;
    private readonly DeleteVehicleCommandHandler _deleteHandler;
    private readonly UpdateVehiclePositionCommandHandler _positionHandler;
    private readonly GetVehicleQueryHandler _getHandler;
    private readonly ListVehiclesQueryHandler _listHandler;
    private readonly Guid _tenantId = Guid.NewGuid();
    private Guid _fleetId;
    private Guid _vehicleId;

    public VehicleUpdateDeleteHandlerTests()
    {
        var options = new DbContextOptionsBuilder<FleetAssetsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db             = new FleetAssetsDbContext(options);
        _updateHandler  = new UpdateVehicleCommandHandler(_db);
        _deleteHandler  = new DeleteVehicleCommandHandler(_db);
        _positionHandler = new UpdateVehiclePositionCommandHandler(_db);
        _getHandler     = new GetVehicleQueryHandler(_db);
        _listHandler    = new ListVehiclesQueryHandler(_db);
    }

    private async Task<(Guid fleetId, Guid vehicleId)> SeedAsync()
    {
        var fleet = await new CreateFleetCommandHandler(_db).Handle(
            new CreateFleetCommand(_tenantId, "Fleet", null), default);

        var limitsClient = Substitute.For<ITenantLimitsClient>();
        limitsClient.GetLimitsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new TenantLimitsResponse(_tenantId, "Starter", 25, 25, true));

        var vehicle = await new CreateVehicleCommandHandler(_db, limitsClient).Handle(
            new CreateVehicleCommand(_tenantId, fleet.Id, "ABC-001", null, "Toyota", "Hilux", 2020), default);

        _fleetId   = fleet.Id;
        _vehicleId = vehicle.Id;
        return (fleet.Id, vehicle.Id);
    }

    [Fact]
    public async Task UpdateVehicle_ShouldChangeStatus()
    {
        await SeedAsync();

        var result = await _updateHandler.Handle(
            new UpdateVehicleCommand(_vehicleId, _tenantId, "XYZ-999", "Ford", "Ranger", 2021, 10000, VehicleStatus.Maintenance), default);

        result.Status.Should().Be("Maintenance");
        result.OdometerKm.Should().Be(10000);
    }

    [Fact]
    public async Task UpdateVehicle_WithWrongTenant_ShouldThrowVehicleNotFoundException()
    {
        await SeedAsync();

        var act = async () => await _updateHandler.Handle(
            new UpdateVehicleCommand(_vehicleId, Guid.NewGuid(), "ABC", "Brand", "Model", 2020, 0, VehicleStatus.Active), default);

        await act.Should().ThrowAsync<VehicleNotFoundException>();
    }

    [Fact]
    public async Task UpdateVehiclePosition_ShouldSetCoordinates()
    {
        await SeedAsync();

        await _positionHandler.Handle(
            new UpdateVehiclePositionCommand(_vehicleId, _tenantId, -74.0060, 40.7128), default);

        var updated = await _getHandler.Handle(new GetVehicleQuery(_vehicleId, _tenantId), default);
        updated.Longitude.Should().BeApproximately(-74.0060, 0.0001);
        updated.Latitude.Should().BeApproximately(40.7128, 0.0001);
    }

    [Fact]
    public async Task DeleteVehicle_ShouldSoftDeleteAndHideFromQueries()
    {
        await SeedAsync();

        await _deleteHandler.Handle(new DeleteVehicleCommand(_vehicleId, _tenantId), default);

        // Global query filter hides soft-deleted vehicles
        var act = async () => await _getHandler.Handle(new GetVehicleQuery(_vehicleId, _tenantId), default);
        await act.Should().ThrowAsync<VehicleNotFoundException>();
    }

    [Fact]
    public async Task DeleteVehicle_TwiceShouldThrowVehicleNotFoundException()
    {
        await SeedAsync();

        await _deleteHandler.Handle(new DeleteVehicleCommand(_vehicleId, _tenantId), default);

        var act = async () => await _deleteHandler.Handle(new DeleteVehicleCommand(_vehicleId, _tenantId), default);
        await act.Should().ThrowAsync<VehicleNotFoundException>();
    }

    [Fact]
    public async Task ListVehicles_WithFleetFilter_ShouldReturnOnlyFleetVehicles()
    {
        await SeedAsync();

        // Add vehicle in second fleet
        var fleet2 = await new CreateFleetCommandHandler(_db).Handle(
            new CreateFleetCommand(_tenantId, "Fleet 2", null), default);

        var limitsClient = Substitute.For<ITenantLimitsClient>();
        limitsClient.GetLimitsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new TenantLimitsResponse(_tenantId, "Starter", 25, 25, true));

        await new CreateVehicleCommandHandler(_db, limitsClient).Handle(
            new CreateVehicleCommand(_tenantId, fleet2.Id, "XYZ-002", null, "Ford", "Ranger", 2021), default);

        var result = await _listHandler.Handle(new ListVehiclesQuery(_tenantId, 1, 20, _fleetId), default);

        result.Items.Should().HaveCount(1);
        result.Items[0].FleetId.Should().Be(_fleetId);
    }

    public void Dispose() => _db.Dispose();
}
