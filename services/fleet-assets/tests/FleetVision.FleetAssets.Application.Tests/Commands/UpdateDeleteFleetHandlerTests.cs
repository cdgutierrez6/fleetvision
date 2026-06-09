using FleetVision.FleetAssets.Application.Fleets.Commands;
using FleetVision.FleetAssets.Application.Fleets.Queries;
using FleetVision.FleetAssets.Domain.Exceptions;
using FleetVision.FleetAssets.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FleetVision.FleetAssets.Application.Tests.Commands;

public sealed class UpdateDeleteFleetHandlerTests : IDisposable
{
    private readonly FleetAssetsDbContext _db;
    private readonly CreateFleetCommandHandler _createHandler;
    private readonly UpdateFleetCommandHandler _updateHandler;
    private readonly DeleteFleetCommandHandler _deleteHandler;
    private readonly GetFleetQueryHandler _getHandler;
    private readonly ListFleetsQueryHandler _listHandler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public UpdateDeleteFleetHandlerTests()
    {
        var options = new DbContextOptionsBuilder<FleetAssetsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db            = new FleetAssetsDbContext(options);
        _createHandler = new CreateFleetCommandHandler(_db);
        _updateHandler = new UpdateFleetCommandHandler(_db);
        _deleteHandler = new DeleteFleetCommandHandler(_db);
        _getHandler    = new GetFleetQueryHandler(_db);
        _listHandler   = new ListFleetsQueryHandler(_db);
    }

    [Fact]
    public async Task UpdateFleet_WithValidData_ShouldChangeNameAndDescription()
    {
        var fleet = await _createHandler.Handle(
            new CreateFleetCommand(_tenantId, "Old Name", "Old desc"), default);

        var result = await _updateHandler.Handle(
            new UpdateFleetCommand(fleet.Id, _tenantId, "New Name", "New desc"), default);

        result.Name.Should().Be("New Name");
        result.Description.Should().Be("New desc");
    }

    [Fact]
    public async Task UpdateFleet_WithWrongTenant_ShouldThrowFleetNotFoundException()
    {
        var fleet = await _createHandler.Handle(
            new CreateFleetCommand(_tenantId, "Fleet", null), default);

        var act = async () => await _updateHandler.Handle(
            new UpdateFleetCommand(fleet.Id, Guid.NewGuid(), "New Name", null), default);

        await act.Should().ThrowAsync<FleetNotFoundException>();
    }

    [Fact]
    public async Task DeleteFleet_ShouldRemoveFromDatabase()
    {
        var fleet = await _createHandler.Handle(
            new CreateFleetCommand(_tenantId, "To Delete", null), default);

        await _deleteHandler.Handle(new DeleteFleetCommand(fleet.Id, _tenantId), default);

        var count = await _db.Fleets.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task DeleteFleet_WithWrongTenant_ShouldThrowFleetNotFoundException()
    {
        var fleet = await _createHandler.Handle(
            new CreateFleetCommand(_tenantId, "Fleet", null), default);

        var act = async () => await _deleteHandler.Handle(
            new DeleteFleetCommand(fleet.Id, Guid.NewGuid()), default);

        await act.Should().ThrowAsync<FleetNotFoundException>();
    }

    [Fact]
    public async Task GetFleet_ShouldReturnCorrectFleet()
    {
        var fleet = await _createHandler.Handle(
            new CreateFleetCommand(_tenantId, "My Fleet", "Desc"), default);

        var result = await _getHandler.Handle(new GetFleetQuery(fleet.Id, _tenantId), default);

        result.Id.Should().Be(fleet.Id);
        result.Name.Should().Be("My Fleet");
    }

    [Fact]
    public async Task GetFleet_WithNonExistentId_ShouldThrowFleetNotFoundException()
    {
        var act = async () => await _getHandler.Handle(
            new GetFleetQuery(Guid.NewGuid(), _tenantId), default);

        await act.Should().ThrowAsync<FleetNotFoundException>();
    }

    [Fact]
    public async Task ListFleets_ShouldReturnOnlyTenantFleets()
    {
        var otherTenant = Guid.NewGuid();

        await _createHandler.Handle(new CreateFleetCommand(_tenantId, "Fleet A", null), default);
        await _createHandler.Handle(new CreateFleetCommand(_tenantId, "Fleet B", null), default);
        await _createHandler.Handle(new CreateFleetCommand(otherTenant, "Other Fleet", null), default);

        var result = await _listHandler.Handle(new ListFleetsQuery(_tenantId, 1, 20), default);

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.Items.Should().AllSatisfy(f => f.TenantId.Should().Be(_tenantId));
    }

    [Fact]
    public async Task ListFleets_Pagination_ShouldReturnCorrectPage()
    {
        for (var i = 0; i < 5; i++)
            await _createHandler.Handle(new CreateFleetCommand(_tenantId, $"Fleet {i}", null), default);

        var result = await _listHandler.Handle(new ListFleetsQuery(_tenantId, 2, 2), default);

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(5);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
    }

    public void Dispose() => _db.Dispose();
}
