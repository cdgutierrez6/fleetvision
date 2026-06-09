using FleetVision.FleetAssets.Application.Drivers.Commands;
using FleetVision.FleetAssets.Application.Drivers.Queries;
using FleetVision.FleetAssets.Domain.Enums;
using FleetVision.FleetAssets.Domain.Exceptions;
using FleetVision.FleetAssets.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FleetVision.FleetAssets.Application.Tests.Commands;

public sealed class DriverHandlerTests : IDisposable
{
    private readonly FleetAssetsDbContext _db;
    private readonly CreateDriverCommandHandler _createHandler;
    private readonly UpdateDriverCommandHandler _updateHandler;
    private readonly DeleteDriverCommandHandler _deleteHandler;
    private readonly GetDriverQueryHandler _getHandler;
    private readonly ListDriversQueryHandler _listHandler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public DriverHandlerTests()
    {
        var options = new DbContextOptionsBuilder<FleetAssetsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db            = new FleetAssetsDbContext(options);
        _createHandler = new CreateDriverCommandHandler(_db);
        _updateHandler = new UpdateDriverCommandHandler(_db);
        _deleteHandler = new DeleteDriverCommandHandler(_db);
        _getHandler    = new GetDriverQueryHandler(_db);
        _listHandler   = new ListDriversQueryHandler(_db);
    }

    [Fact]
    public async Task CreateDriver_WithValidData_ShouldReturnDriverDto()
    {
        var result = await _createHandler.Handle(
            new CreateDriverCommand(_tenantId, "Juan Pérez", "DL-001", "+57-300", "juan@corp.com"), default);

        result.Id.Should().NotBeEmpty();
        result.FullName.Should().Be("Juan Pérez");
        result.LicenseNumber.Should().Be("DL-001");
        result.Status.Should().Be("Active");
    }

    [Fact]
    public async Task UpdateDriver_ShouldChangeStatusToInactive()
    {
        var driver = await _createHandler.Handle(
            new CreateDriverCommand(_tenantId, "Ana López", "DL-002"), default);

        var result = await _updateHandler.Handle(
            new UpdateDriverCommand(driver.Id, _tenantId, "Ana López", "DL-002", null, null, DriverStatus.Inactive), default);

        result.Status.Should().Be("Inactive");
    }

    [Fact]
    public async Task UpdateDriver_WithWrongTenant_ShouldThrowDriverNotFoundException()
    {
        var driver = await _createHandler.Handle(
            new CreateDriverCommand(_tenantId, "Name", "LIC"), default);

        var act = async () => await _updateHandler.Handle(
            new UpdateDriverCommand(driver.Id, Guid.NewGuid(), "Name", "LIC", null, null, DriverStatus.Active), default);

        await act.Should().ThrowAsync<DriverNotFoundException>();
    }

    [Fact]
    public async Task DeleteDriver_ShouldSoftDelete()
    {
        var driver = await _createHandler.Handle(
            new CreateDriverCommand(_tenantId, "Carlos", "DL-003"), default);

        await _deleteHandler.Handle(new DeleteDriverCommand(driver.Id, _tenantId), default);

        // Global query filter IsDeleted=false → driver should not be found
        var act = async () => await _getHandler.Handle(new GetDriverQuery(driver.Id, _tenantId), default);
        await act.Should().ThrowAsync<DriverNotFoundException>();
    }

    [Fact]
    public async Task DeleteDriver_TwiceShouldThrowDriverNotFoundException()
    {
        var driver = await _createHandler.Handle(
            new CreateDriverCommand(_tenantId, "Carlos", "DL-003"), default);

        await _deleteHandler.Handle(new DeleteDriverCommand(driver.Id, _tenantId), default);

        var act = async () => await _deleteHandler.Handle(new DeleteDriverCommand(driver.Id, _tenantId), default);
        await act.Should().ThrowAsync<DriverNotFoundException>();
    }

    [Fact]
    public async Task GetDriver_ShouldReturnCorrectDriver()
    {
        var driver = await _createHandler.Handle(
            new CreateDriverCommand(_tenantId, "María García", "DL-010", "+57-311", "maria@corp.com"), default);

        var result = await _getHandler.Handle(new GetDriverQuery(driver.Id, _tenantId), default);

        result.FullName.Should().Be("María García");
        result.Email.Should().Be("maria@corp.com");
    }

    [Fact]
    public async Task ListDrivers_ShouldReturnOnlyTenantDrivers()
    {
        var other = Guid.NewGuid();
        await _createHandler.Handle(new CreateDriverCommand(_tenantId, "Driver A", "LIC-A"), default);
        await _createHandler.Handle(new CreateDriverCommand(_tenantId, "Driver B", "LIC-B"), default);
        await _createHandler.Handle(new CreateDriverCommand(other, "Driver C", "LIC-C"), default);

        var result = await _listHandler.Handle(new ListDriversQuery(_tenantId, 1, 20), default);

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
    }

    public void Dispose() => _db.Dispose();
}
