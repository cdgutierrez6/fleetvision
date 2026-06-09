using FleetVision.PredictiveMaintenance.Application.Commands;
using FleetVision.PredictiveMaintenance.Domain.Entities;
using FleetVision.PredictiveMaintenance.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace FleetVision.PredictiveMaintenance.Application.Tests;

public sealed class CompleteMaintenanceHandlerTests
{
    private static readonly Guid TenantId  = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly Guid RecordId  = Guid.NewGuid();

    [Fact]
    public async Task Returns_false_when_record_not_found()
    {
        var repo   = new Mock<IMaintenanceRepository>();
        var cache  = new Mock<IOdometerCache>();

        repo.Setup(r => r.GetByIdAsync(RecordId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MaintenanceRecord?)null);

        var handler = new CompleteMaintenanceHandler(repo.Object, cache.Object);
        var result  = await handler.Handle(new CompleteMaintenanceCommand(RecordId, TenantId), CancellationToken.None);

        result.Should().BeFalse();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        cache.Verify(c => c.ResetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Returns_true_resolves_record_and_resets_odometer()
    {
        var repo   = new Mock<IMaintenanceRepository>();
        var cache  = new Mock<IOdometerCache>();
        var record = MaintenanceRecord.CreateScheduled(TenantId, VehicleId, "ODOMETER");

        repo.Setup(r => r.GetByIdAsync(record.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var handler = new CompleteMaintenanceHandler(repo.Object, cache.Object);
        var result  = await handler.Handle(new CompleteMaintenanceCommand(record.Id, TenantId), CancellationToken.None);

        result.Should().BeTrue();
        record.RecordType.Should().Be("COMPLETED");
        record.ResolvedAt.Should().NotBeNull();
        cache.Verify(c => c.ResetAsync(TenantId, VehicleId, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Critical_alert_record_can_be_completed()
    {
        var repo   = new Mock<IMaintenanceRepository>();
        var cache  = new Mock<IOdometerCache>();
        var record = MaintenanceRecord.CreateCriticalAlert(TenantId, VehicleId, "P0300");

        repo.Setup(r => r.GetByIdAsync(record.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var handler = new CompleteMaintenanceHandler(repo.Object, cache.Object);
        var result  = await handler.Handle(new CompleteMaintenanceCommand(record.Id, TenantId), CancellationToken.None);

        result.Should().BeTrue();
        record.RecordType.Should().Be("COMPLETED");
    }

    [Fact]
    public async Task Tenant_isolation_enforced_different_tenant_returns_false()
    {
        // The repository enforces tenant isolation via RLS.
        // From the handler's perspective: if GetByIdAsync returns null, handler returns false.
        var repo         = new Mock<IMaintenanceRepository>();
        var cache        = new Mock<IOdometerCache>();
        var otherTenant  = Guid.NewGuid();

        repo.Setup(r => r.GetByIdAsync(RecordId, otherTenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MaintenanceRecord?)null); // RLS blocks cross-tenant access

        var handler = new CompleteMaintenanceHandler(repo.Object, cache.Object);
        var result  = await handler.Handle(new CompleteMaintenanceCommand(RecordId, otherTenant), CancellationToken.None);

        result.Should().BeFalse();
    }
}
