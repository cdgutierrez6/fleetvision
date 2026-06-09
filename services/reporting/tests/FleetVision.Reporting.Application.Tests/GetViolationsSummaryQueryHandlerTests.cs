using FleetVision.Reporting.Application.Common.Dtos;
using FleetVision.Reporting.Application.Common.Interfaces;
using FleetVision.Reporting.Application.Queries.GetViolationsSummary;
using FluentAssertions;
using NSubstitute;

namespace FleetVision.Reporting.Application.Tests;

public sealed class GetViolationsSummaryQueryHandlerTests
{
    private readonly IViolationsReader _reader = Substitute.For<IViolationsReader>();
    private readonly GetViolationsSummaryQueryHandler _handler;

    public GetViolationsSummaryQueryHandlerTests()
        => _handler = new GetViolationsSummaryQueryHandler(_reader);

    [Fact]
    public async Task Handle_ReturnsSummaryFromReader()
    {
        var tenantId = Guid.NewGuid();
        var expected = new ViolationsSummaryDto(
            42,
            new[] { new ViolationByTypeDto("SpeedExceeded", 30, 71.4) },
            new[] { new ViolationByVehicleDto(Guid.NewGuid(), 15) });

        _reader.GetSummaryAsync(tenantId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
               .Returns(expected);

        var result = await _handler.Handle(new GetViolationsSummaryQuery(tenantId, "30d"), default);

        result.Should().Be(expected);
        result.TotalViolations.Should().Be(42);
    }

    [Fact]
    public async Task Handle_EmptyViolations_ReturnsTotalZero()
    {
        var tenantId = Guid.NewGuid();
        var empty = new ViolationsSummaryDto(0, Array.Empty<ViolationByTypeDto>(), Array.Empty<ViolationByVehicleDto>());
        _reader.GetSummaryAsync(tenantId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
               .Returns(empty);

        var result = await _handler.Handle(new GetViolationsSummaryQuery(tenantId, "7d"), default);

        result.TotalViolations.Should().Be(0);
        result.ByType.Should().BeEmpty();
    }
}
