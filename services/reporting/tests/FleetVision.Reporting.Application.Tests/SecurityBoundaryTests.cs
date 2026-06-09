using FleetVision.Reporting.Application.Common.Dtos;
using FleetVision.Reporting.Application.Common.Interfaces;
using FleetVision.Reporting.Application.Queries.GetVehicleHistory;
using FleetVision.Reporting.Application.Queries.GetFleetKpis;
using FluentAssertions;
using NSubstitute;

namespace FleetVision.Reporting.Application.Tests;

// Verifies that tenant_id isolation is enforced at the handler level.
// The actual SQL guard (AND tenant_id = $2) is the enforcement in TimeSeriesReader;
// these tests verify handlers always forward the correct tenantId to the reader.
public sealed class SecurityBoundaryTests
{
    [Fact]
    public async Task GetVehicleHistory_AlwaysPassesTenantIdToReader()
    {
        var reader    = Substitute.For<ITimeSeriesReader>();
        var tenantId  = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var wrongId   = Guid.NewGuid();

        reader.GetVehicleHistoryAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
              .Returns(Array.Empty<PositionPointDto>());

        var handler = new GetVehicleHistoryQueryHandler(reader);
        await handler.Handle(new GetVehicleHistoryQuery(tenantId, vehicleId, 24), default);

        // Must be called with the exact tenantId from the query — never wrongId or Guid.Empty
        await reader.Received(1).GetVehicleHistoryAsync(
            tenantId, vehicleId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());

        await reader.DidNotReceive().GetVehicleHistoryAsync(
            wrongId, Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetFleetKpis_AlwaysPassesTenantIdToReader()
    {
        var reader   = Substitute.For<ITimeSeriesReader>();
        var tenantId = Guid.NewGuid();
        var kpis     = new FleetKpisDto(0, 0, 0, 0, 0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        reader.GetFleetKpisAsync(tenantId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
              .Returns(kpis);

        var handler = new GetFleetKpisQueryHandler(reader);
        await handler.Handle(new GetFleetKpisQuery(tenantId, "30d"), default);

        await reader.Received(1).GetFleetKpisAsync(
            tenantId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetVehicleHistory_HoursClampedTo168Max()
    {
        // Hours > 168 would allow reading a year of GPS data in one call.
        // Verify the clamp at handler level — before the query reaches the DB.
        var reader = Substitute.For<ITimeSeriesReader>();
        reader.GetVehicleHistoryAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
              .Returns(Array.Empty<PositionPointDto>());

        var handler = new GetVehicleHistoryQueryHandler(reader);
        await handler.Handle(new GetVehicleHistoryQuery(Guid.NewGuid(), Guid.NewGuid(), 99999), default);

        // The from timestamp must be AT MOST 168 hours ago, never more.
        await reader.Received(1).GetVehicleHistoryAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Is<DateTimeOffset>(from =>
                (DateTimeOffset.UtcNow - from).TotalHours <= 169), // small buffer for test timing
            Arg.Any<CancellationToken>());
    }
}
