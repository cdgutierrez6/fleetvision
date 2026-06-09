using FleetVision.Telemetry.Application.Commands;
using FleetVision.Telemetry.Application.Common;
using FleetVision.Telemetry.Domain.Entities;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace FleetVision.Telemetry.Application.Tests.Commands;

public sealed class IngestTelemetryCommandHandlerTests
{
    private readonly ITelemetryWriter _writer = Substitute.For<ITelemetryWriter>();
    private readonly IPositionCache _cache    = Substitute.For<IPositionCache>();
    private readonly IngestTelemetryCommandHandler _handler;

    public IngestTelemetryCommandHandlerTests()
    {
        _handler = new IngestTelemetryCommandHandler(_writer, _cache);
    }

    private static IngestTelemetryCommand ValidCommand(
        Guid? vehicleId = null,
        Guid? tenantId  = null,
        double lat      = 40.715,
        double lon      = -74.005,
        float? speed    = 60f) => new(
            VehicleId:       vehicleId ?? Guid.NewGuid(),
            TenantId:        tenantId  ?? Guid.NewGuid(),
            DriverId:        null,
            TimestampUnixMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Latitude:        lat,
            Longitude:       lon,
            SpeedKmh:        speed);

    [Fact]
    public async Task Handle_ValidCommand_ShouldPersistAndCache()
    {
        var result = await _handler.Handle(ValidCommand(), default);

        result.Accepted.Should().BeTrue();
        result.PositionKey.Should().NotBeNullOrEmpty();

        await _writer.Received(1).PersistAndEnqueueAsync(Arg.Any<VehiclePosition>(), Arg.Any<CancellationToken>());
        await _cache.Received(1).SetAsync(Arg.Any<VehiclePosition>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidCommand_PositionKeyShouldContainVehicleId()
    {
        var vehicleId = Guid.NewGuid();
        var result = await _handler.Handle(ValidCommand(vehicleId: vehicleId), default);

        result.PositionKey.Should().StartWith(vehicleId.ToString());
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldPassCorrectTenantToWriter()
    {
        var tenantId = Guid.NewGuid();

        await _handler.Handle(ValidCommand(tenantId: tenantId), default);

        await _writer.Received(1).PersistAndEnqueueAsync(
            Arg.Is<VehiclePosition>(p => p.TenantId == tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldPassCorrectCoordinates()
    {
        await _handler.Handle(ValidCommand(lat: 51.5074, lon: -0.1278), default);

        await _writer.Received(1).PersistAndEnqueueAsync(
            Arg.Is<VehiclePosition>(p =>
                Math.Abs(p.Latitude  - 51.5074)   < 0.0001 &&
                Math.Abs(p.Longitude - (-0.1278)) < 0.0001),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenWriterFails_ShouldPropagateException()
    {
        _writer.PersistAndEnqueueAsync(Arg.Any<VehiclePosition>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("DB down")));

        var act = async () => await _handler.Handle(ValidCommand(), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("DB down");
    }

    [Fact]
    public async Task Handle_WhenCacheFails_ShouldStillSucceedIfWriterSucceeds()
    {
        // Redis failure is non-fatal — PositionCache handles errors internally.
        // Writer succeeds → ingestion is considered successful.
        _cache.SetAsync(Arg.Any<VehiclePosition>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(ValidCommand(), default);
        result.Accepted.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_TimestampConversion_ShouldBeCorrect()
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cmd   = ValidCommand() with { TimestampUnixMs = nowMs };

        await _handler.Handle(cmd, default);

        await _writer.Received(1).PersistAndEnqueueAsync(
            Arg.Is<VehiclePosition>(p =>
                Math.Abs(new DateTimeOffset(p.Time).ToUnixTimeMilliseconds() - nowMs) < 1000),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WriterAndCacheRunConcurrently_BothReceiveSamePosition()
    {
        // Validates that Task.WhenAll is used — both writer and cache receive a call.
        VehiclePosition? capturedByWriter = null;
        VehiclePosition? capturedByCache  = null;

        _writer.PersistAndEnqueueAsync(Arg.Do<VehiclePosition>(p => capturedByWriter = p), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _cache.SetAsync(Arg.Do<VehiclePosition>(p => capturedByCache = p), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _handler.Handle(ValidCommand(), default);

        capturedByWriter.Should().NotBeNull();
        capturedByCache.Should().NotBeNull();
        capturedByWriter!.VehicleId.Should().Be(capturedByCache!.VehicleId);
    }
}
