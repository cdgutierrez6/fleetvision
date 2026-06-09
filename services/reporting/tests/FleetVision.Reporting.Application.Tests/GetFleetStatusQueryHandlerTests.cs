using FleetVision.Reporting.Application.Common.Dtos;
using FleetVision.Reporting.Application.Common.Interfaces;
using FleetVision.Reporting.Application.Queries.GetFleetStatus;
using FluentAssertions;
using NSubstitute;

namespace FleetVision.Reporting.Application.Tests;

public sealed class GetFleetStatusQueryHandlerTests
{
    private readonly IFleetStatusReader _reader = Substitute.For<IFleetStatusReader>();
    private readonly GetFleetStatusQueryHandler _handler;

    public GetFleetStatusQueryHandlerTests()
        => _handler = new GetFleetStatusQueryHandler(_reader);

    [Fact]
    public async Task Handle_ReturnsDtoFromReader()
    {
        var tenantId = Guid.NewGuid();
        var expected = new FleetStatusDto(Total: 10, Active: 7, Maintenance: 2, Inactive: 1);
        _reader.GetFleetStatusAsync(tenantId, Arg.Any<CancellationToken>()).Returns(expected);

        var result = await _handler.Handle(new GetFleetStatusQuery(tenantId), default);

        result.Should().Be(expected);
        result.Total.Should().Be(10);
    }

    [Fact]
    public async Task Handle_EmptyFleet_ReturnsTotalZero()
    {
        var tenantId = Guid.NewGuid();
        _reader.GetFleetStatusAsync(tenantId, Arg.Any<CancellationToken>())
               .Returns(new FleetStatusDto(0, 0, 0, 0));

        var result = await _handler.Handle(new GetFleetStatusQuery(tenantId), default);

        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PassesTenantIdToReader()
    {
        var tenantId = Guid.NewGuid();
        _reader.GetFleetStatusAsync(tenantId, Arg.Any<CancellationToken>())
               .Returns(new FleetStatusDto(0, 0, 0, 0));

        await _handler.Handle(new GetFleetStatusQuery(tenantId), default);

        await _reader.Received(1).GetFleetStatusAsync(tenantId, Arg.Any<CancellationToken>());
    }
}
