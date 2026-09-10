using FleetVision.FleetAssets.Application.Common;
using FleetVision.FleetAssets.Application.Drivers.Commands;
using FleetVision.FleetAssets.Application.Fleets.Commands;
using FleetVision.FleetAssets.Application.VehicleAssignments.Commands;
using FleetVision.FleetAssets.Application.VehicleAssignments.Queries;
using FleetVision.FleetAssets.Application.Vehicles.Commands;
using FleetVision.FleetAssets.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace FleetVision.FleetAssets.Application.Tests.Queries;

// Cubre ListVehicleAssignmentsQueryHandler con EF InMemory, sembrando via los handlers reales
// (mismo patron que los *HandlerTests). Solo puede haber una asignacion activa por vehiculo
// (EndedAt == null), por lo que las multiples asignaciones se generan con el ciclo Create/Close.
//
// Nota de orden: el handler hace OrderByDescending(a => a.StartedAt), pero StartedAt = DateTime.UtcNow
// dentro de VehicleAssignment.Create (no inyectable). Timestamps casi simultaneos hacen fragil una
// asercion de orden estricto, por lo que NO se afirma el orden exacto: se afirman Total, tamanos de
// pagina y pertenencia de Ids/VehicleId. Estos casos cruzan todas las ramas del handler
// (Where, CountAsync, OrderByDescending, Skip/Take, Select ToDto) sin depender del reloj.
public sealed class ListVehicleAssignmentsHandlerTests : IDisposable
{
    private readonly FleetAssetsDbContext _db;
    private readonly ListVehicleAssignmentsQueryHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public ListVehicleAssignmentsHandlerTests()
    {
        var options = new DbContextOptionsBuilder<FleetAssetsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db      = new FleetAssetsDbContext(options);
        _handler = new ListVehicleAssignmentsQueryHandler(_db);
    }

    // ------------------------------------------------------------------
    // Helpers de siembra (usan handlers reales)
    // ------------------------------------------------------------------
    private async Task<Guid> SeedVehicleAsync(Guid tenantId, string plate = "ABC-123")
    {
        var fleet = await new CreateFleetCommandHandler(_db)
            .Handle(new CreateFleetCommand(tenantId, "Fleet", null), default);

        var limitsClient = Substitute.For<ITenantLimitsClient>();
        limitsClient.GetLimitsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new TenantLimitsResponse(tenantId, "Starter", 25, 25, true));

        var vehicle = await new CreateVehicleCommandHandler(_db, limitsClient)
            .Handle(new CreateVehicleCommand(tenantId, fleet.Id, plate, null, "Brand", "Model", 2020), default);

        return vehicle.Id;
    }

    private async Task<Guid> SeedDriverAsync(Guid tenantId, string license)
    {
        var driver = await new CreateDriverCommandHandler(_db)
            .Handle(new CreateDriverCommand(tenantId, "Driver", license), default);
        return driver.Id;
    }

    // Genera 'count' asignaciones historicas sobre el mismo vehiculo: crea y cierra
    // (count - 1) veces y deja 1 activa. Requiere solo un driver activo.
    private async Task SeedAssignmentsAsync(Guid tenantId, Guid vehicleId, Guid driverId, int count)
    {
        var create = new CreateVehicleAssignmentCommandHandler(_db);
        var close  = new CloseVehicleAssignmentCommandHandler(_db);

        for (var i = 0; i < count; i++)
        {
            await create.Handle(new CreateVehicleAssignmentCommand(tenantId, vehicleId, driverId), default);
            if (i < count - 1)
                await close.Handle(new CloseVehicleAssignmentCommand(tenantId, vehicleId), default);
        }
    }

    // ------------------------------------------------------------------
    // Casos
    // ------------------------------------------------------------------
    [Fact]
    public async Task Handle_ConAsignaciones_DevuelvePagedResultConTotalCorrecto()
    {
        var vehicleId = await SeedVehicleAsync(_tenantId);
        var driverId  = await SeedDriverAsync(_tenantId, "LIC-001");
        await SeedAssignmentsAsync(_tenantId, vehicleId, driverId, count: 3);

        var result = await _handler.Handle(
            new ListVehicleAssignmentsQuery(_tenantId, vehicleId, Page: 1, PageSize: 20), default);

        result.Total.Should().Be(3);
        result.Items.Count.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.Items.Should().OnlyContain(a => a.VehicleId == vehicleId);
    }

    [Fact]
    public async Task Handle_Paginacion_CortaCorrectamente()
    {
        var vehicleId = await SeedVehicleAsync(_tenantId);
        var driverId  = await SeedDriverAsync(_tenantId, "LIC-001");
        await SeedAssignmentsAsync(_tenantId, vehicleId, driverId, count: 3);

        var page1 = await _handler.Handle(
            new ListVehicleAssignmentsQuery(_tenantId, vehicleId, Page: 1, PageSize: 2), default);
        page1.Items.Count.Should().Be(2);
        page1.Total.Should().Be(3);

        var page2 = await _handler.Handle(
            new ListVehicleAssignmentsQuery(_tenantId, vehicleId, Page: 2, PageSize: 2), default);
        page2.Items.Count.Should().Be(1);
        page2.Total.Should().Be(3);

        // Skip/Take reales: sin solapamiento entre paginas.
        page1.Items.Select(a => a.Id).Should().NotIntersectWith(page2.Items.Select(a => a.Id));
    }

    [Fact]
    public async Task Handle_FiltraPorVehicleId()
    {
        var driverId  = await SeedDriverAsync(_tenantId, "LIC-001");
        var vehicleA  = await SeedVehicleAsync(_tenantId, "AAA-111");
        var vehicleB  = await SeedVehicleAsync(_tenantId, "BBB-222");

        await SeedAssignmentsAsync(_tenantId, vehicleA, driverId, count: 2);
        await SeedAssignmentsAsync(_tenantId, vehicleB, driverId, count: 3);

        var result = await _handler.Handle(
            new ListVehicleAssignmentsQuery(_tenantId, vehicleA, Page: 1, PageSize: 20), default);

        result.Total.Should().Be(2);
        result.Items.Should().OnlyContain(a => a.VehicleId == vehicleA);
    }

    [Fact]
    public async Task Handle_FiltraPorTenantId()
    {
        // Asignacion en _tenantId.
        var driver = await SeedDriverAsync(_tenantId, "LIC-001");
        var vehicle = await SeedVehicleAsync(_tenantId, "AAA-111");
        await SeedAssignmentsAsync(_tenantId, vehicle, driver, count: 1);

        // Asignacion en otro tenant, con su propio fleet/vehicle/driver.
        var otherTenant = Guid.NewGuid();
        var otherDriver = await SeedDriverAsync(otherTenant, "LIC-999");
        var otherVehicle = await SeedVehicleAsync(otherTenant, "ZZZ-999");
        await SeedAssignmentsAsync(otherTenant, otherVehicle, otherDriver, count: 1);

        // Consulta cruzada: tenant _tenantId pero vehiculo del otro tenant -> aislado por TenantId.
        var result = await _handler.Handle(
            new ListVehicleAssignmentsQuery(_tenantId, otherVehicle, Page: 1, PageSize: 20), default);

        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SinAsignaciones_DevuelveVacio()
    {
        var vehicleId = await SeedVehicleAsync(_tenantId);

        var result = await _handler.Handle(
            new ListVehicleAssignmentsQuery(_tenantId, vehicleId, Page: 1, PageSize: 20), default);

        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    public void Dispose() => _db.Dispose();
}
