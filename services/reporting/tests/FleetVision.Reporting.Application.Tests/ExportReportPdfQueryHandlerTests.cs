using FleetVision.Reporting.Application.Common.Dtos;
using FleetVision.Reporting.Application.Common.Interfaces;
using FleetVision.Reporting.Application.Queries.ExportReportPdf;
using FluentAssertions;
using NSubstitute;

namespace FleetVision.Reporting.Application.Tests;

public sealed class ExportReportPdfQueryHandlerTests
{
    private readonly ITimeSeriesReader  _timeSeries  = Substitute.For<ITimeSeriesReader>();
    private readonly IViolationsReader  _violations  = Substitute.For<IViolationsReader>();
    private readonly IFleetStatusReader _fleet       = Substitute.For<IFleetStatusReader>();
    private readonly IPdfGenerator      _pdf         = Substitute.For<IPdfGenerator>();
    private readonly ExportReportPdfQueryHandler _handler;

    public ExportReportPdfQueryHandlerTests()
        => _handler = new ExportReportPdfQueryHandler(_timeSeries, _violations, _fleet, _pdf);

    [Fact]
    public async Task Handle_CallsAllReadersAndReturnsPdfBytes()
    {
        var tenantId  = Guid.NewGuid();
        var kpis      = new FleetKpisDto(3, 500, 60, 120, 10000, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var viols     = new ViolationsSummaryDto(0, Array.Empty<ViolationByTypeDto>(), Array.Empty<ViolationByVehicleDto>());
        var status    = new FleetStatusDto(3, 2, 1, 0);
        var pdfBytes  = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF magic bytes

        _timeSeries.GetFleetKpisAsync(tenantId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
                   .Returns(kpis);
        _violations.GetSummaryAsync(tenantId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
                   .Returns(viols);
        _fleet.GetFleetStatusAsync(tenantId, Arg.Any<CancellationToken>()).Returns(status);
        _pdf.GenerateFleetReportAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                                      kpis, viols, status, Arg.Any<CancellationToken>())
            .Returns(pdfBytes);

        var result = await _handler.Handle(
            new ExportReportPdfQuery(tenantId, "Acme Corp", "30d"), default);

        result.Should().BeEquivalentTo(pdfBytes);
        await _timeSeries.Received(1).GetFleetKpisAsync(tenantId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _violations.Received(1).GetSummaryAsync(tenantId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _fleet.Received(1).GetFleetStatusAsync(tenantId, Arg.Any<CancellationToken>());
    }
}
