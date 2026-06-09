using FleetVision.Reporting.Application.Common.Dtos;
using FleetVision.Reporting.Application.Common.Interfaces;
using FleetVision.Reporting.Application.Queries.GetFleetKpis;
using FluentAssertions;
using NSubstitute;

namespace FleetVision.Reporting.Application.Tests;

public sealed class GetFleetKpisQueryHandlerTests
{
    private readonly ITimeSeriesReader _reader = Substitute.For<ITimeSeriesReader>();
    private readonly GetFleetKpisQueryHandler _handler;

    public GetFleetKpisQueryHandlerTests()
        => _handler = new GetFleetKpisQueryHandler(_reader);

    [Fact]
    public async Task Handle_ReturnsKpisFromReader()
    {
        var tenantId = Guid.NewGuid();
        var expected = new FleetKpisDto(5, 1200.5, 62.3, 130.0, 48000,
            DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow);

        _reader.GetFleetKpisAsync(tenantId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
               .Returns(expected);

        var result = await _handler.Handle(new GetFleetKpisQuery(tenantId, "30d"), default);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("7d",  7)]
    [InlineData("30d", 30)]
    [InlineData("90d", 90)]
    [InlineData("bad", 30)] // unknown → defaults to 30d
    public void ParseRange_ReturnsCorrectDays(string range, int expectedDays)
    {
        var (from, to) = GetFleetKpisQueryHandler.ParseRange(range);
        var diff = (to - from).TotalDays;
        diff.Should().BeApproximately(expectedDays, precision: 0.1);
    }

    [Fact]
    public async Task Handle_PassesTenantIdToReader()
    {
        var tenantId = Guid.NewGuid();
        var kpis = new FleetKpisDto(0, 0, 0, 0, 0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _reader.GetFleetKpisAsync(tenantId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
               .Returns(kpis);

        await _handler.Handle(new GetFleetKpisQuery(tenantId, "7d"), default);

        await _reader.Received(1)
            .GetFleetKpisAsync(tenantId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }
}
