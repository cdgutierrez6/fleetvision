using FleetVision.PredictiveMaintenance.Application.Services;
using FleetVision.PredictiveMaintenance.Domain.Entities;
using FleetVision.PredictiveMaintenance.Domain.Interfaces;
using FleetVision.PredictiveMaintenance.Domain.Rules;
using FleetVision.PredictiveMaintenance.Domain.Services;
using FleetVision.PredictiveMaintenance.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FleetVision.PredictiveMaintenance.Application.Tests;

public sealed class MaintenanceOrchestratorTests
{
    private static readonly Guid TenantId  = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();

    private static TelemetryReading Reading(string? obd2 = null) => new(
        TenantId, VehicleId, 4.7, -74.0, 60f, 0.5m, obd2, DateTime.UtcNow);

    private static MaintenanceOrchestrator BuildOrchestrator(
        Mock<IMaintenanceRepository> repo,
        Mock<IOdometerCache> cache,
        Mock<IMaintenanceOutboxEnqueuer> outbox,
        decimal odometerKm = 100m)
    {
        cache.Setup(c => c.GetAndIncrementAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(),
                It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OdometerSnapshot.FromKm(odometerKm));

        repo.Setup(r => r.GetLastMaintenanceAtAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        var engine = new MaintenanceRuleEngine(new IMaintenanceRule[]
        {
            new OBD2CriticalRule(), new OBD2WarningRule(), new OdometerRule(), new TimeBasedRule(),
        });

        return new MaintenanceOrchestrator(
            engine, repo.Object, cache.Object, outbox.Object,
            Options.Create(new MaintenanceOptions()),
            NullLogger<MaintenanceOrchestrator>.Instance);
    }

    [Fact]
    public async Task Normal_telemetry_no_records_created()
    {
        var repo   = new Mock<IMaintenanceRepository>();
        var cache  = new Mock<IOdometerCache>();
        var outbox = new Mock<IMaintenanceOutboxEnqueuer>();
        var sut    = BuildOrchestrator(repo, cache, outbox);

        await sut.ProcessAsync(Reading(), 1L, CancellationToken.None);

        repo.Verify(r => r.AddAsync(It.IsAny<MaintenanceRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        outbox.Verify(o => o.EnqueueAlert(It.IsAny<MaintenanceRecord>()), Times.Never);
    }

    [Fact]
    public async Task Critical_obd2_creates_alert_and_enqueues_alert()
    {
        var repo   = new Mock<IMaintenanceRepository>();
        var cache  = new Mock<IOdometerCache>();
        var outbox = new Mock<IMaintenanceOutboxEnqueuer>();
        var sut    = BuildOrchestrator(repo, cache, outbox);

        await sut.ProcessAsync(Reading("P0300"), 1L, CancellationToken.None);

        repo.Verify(r => r.AddAsync(
            It.Is<MaintenanceRecord>(rec => rec.RecordType == "CRITICAL_ALERT"),
            It.IsAny<CancellationToken>()), Times.Once);
        outbox.Verify(o => o.EnqueueAlert(It.IsAny<MaintenanceRecord>()), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Odometer_threshold_creates_scheduled_and_resets_odometer()
    {
        var repo   = new Mock<IMaintenanceRepository>();
        var cache  = new Mock<IOdometerCache>();
        var outbox = new Mock<IMaintenanceOutboxEnqueuer>();
        var sut    = BuildOrchestrator(repo, cache, outbox, odometerKm: 10_001m);

        await sut.ProcessAsync(Reading(), 1L, CancellationToken.None);

        outbox.Verify(o => o.EnqueueScheduled(
            It.Is<MaintenanceRecord>(r => r.TriggeredBy == "ODOMETER")), Times.Once);
        cache.Verify(c => c.ResetAsync(TenantId, VehicleId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Redis_degraded_does_not_throw_and_no_odometer_alert()
    {
        var repo   = new Mock<IMaintenanceRepository>();
        var cache  = new Mock<IOdometerCache>();
        var outbox = new Mock<IMaintenanceOutboxEnqueuer>();

        cache.Setup(c => c.GetAndIncrementAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<decimal>(),
                It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OdometerSnapshot.Unknown);

        repo.Setup(r => r.GetLastMaintenanceAtAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        var engine = new MaintenanceRuleEngine(new IMaintenanceRule[] { new OdometerRule() });
        var sut    = new MaintenanceOrchestrator(
            engine, repo.Object, cache.Object, outbox.Object,
            Options.Create(new MaintenanceOptions()),
            NullLogger<MaintenanceOrchestrator>.Instance);

        var act = async () => await sut.ProcessAsync(Reading(), 1L, CancellationToken.None);
        await act.Should().NotThrowAsync();

        outbox.Verify(o => o.EnqueueScheduled(It.IsAny<MaintenanceRecord>()), Times.Never);
    }
}
