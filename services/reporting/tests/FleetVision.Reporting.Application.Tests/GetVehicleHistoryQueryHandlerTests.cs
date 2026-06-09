using FleetVision.Reporting.Application.Common.Dtos;
using FleetVision.Reporting.Application.Common.Interfaces;
using FleetVision.Reporting.Application.Queries.GetVehicleHistory;
using FluentAssertions;
using NSubstitute;

namespace FleetVision.Reporting.Application.Tests;

public sealed class GetVehicleHistoryQueryHandlerTests
{
    private readonly ITimeSeriesReader _reader = Substitute.For<ITimeSeriesReader>();
    private readonly GetVehicleHistoryQueryHandler _handler;

    public GetVehicleHistoryQueryHandlerTests()
        => _handler = new GetVehicleHistoryQueryHandler(_reader);

    [Fact]
    public async Task Handle_ReturnsPositionPoints()
    {
        var tenantId  = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var points = new[]
        {
            new PositionPointDto(4.71, -74.07, 60, 90, 12000, DateTimeOffset.UtcNow.AddHours(-1)),
            new PositionPointDto(4.72, -74.08, 55, 92, 12050, DateTimeOffset.UtcNow),
        };

        _reader.GetVehicleHistoryAsync(tenantId, vehicleId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
               .Returns(points);

        var result = await _handler.Handle(
            new GetVehicleHistoryQuery(tenantId, vehicleId, 24), default);

        result.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(0,   1)]   // clamps up to 1
    [InlineData(200, 168)] // clamps down to 168
    [InlineData(24,  24)]  // in range — no change
    public async Task Handle_ClampsHours(int inputHours, int expectedClampedHours)
    {
        var tenantId  = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        _reader.GetVehicleHistoryAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
               .Returns(Array.Empty<PositionPointDto>());

        await _handler.Handle(new GetVehicleHistoryQuery(tenantId, vehicleId, inputHours), default);

        await _reader.Received(1).GetVehicleHistoryAsync(
            tenantId,
            vehicleId,
            Arg.Is<DateTimeOffset>(from =>
                Math.Abs((DateTimeOffset.UtcNow - from).TotalHours - expectedClampedHours) < 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownVehicle_ReturnsEmpty()
    {
        _reader.GetVehicleHistoryAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
               .Returns(Array.Empty<PositionPointDto>());

        var result = await _handler.Handle(
            new GetVehicleHistoryQuery(Guid.NewGuid(), Guid.NewGuid(), 24), default);

        result.Should().BeEmpty();
    }
}
