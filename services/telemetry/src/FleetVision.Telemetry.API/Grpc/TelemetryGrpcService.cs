using FleetVision.Proto.Telemetry;
using FleetVision.Telemetry.Application.Commands;
using Grpc.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace FleetVision.Telemetry.API.Grpc;

[Authorize]
public sealed class TelemetryGrpcService : TelemetryIngestionService.TelemetryIngestionServiceBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<TelemetryGrpcService> _logger;

    public TelemetryGrpcService(IMediator mediator, ILogger<TelemetryGrpcService> logger)
    {
        _mediator = mediator;
        _logger   = logger;
    }

    public override async Task<IngestResponse> IngestBatch(
        TelemetryBatch request,
        ServerCallContext context)
    {
        var tenantId = GetTenantId(context);

        var accepted = 0;
        var rejected = 0;

        foreach (var ping in request.Pings)
        {
            try
            {
                await IngestSinglePingAsync(ping, tenantId, context.CancellationToken);
                accepted++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Rejected ping for vehicle {VehicleId}", ping.VehicleId);
                rejected++;
            }
        }

        return new IngestResponse
        {
            Accepted = accepted,
            Rejected = rejected,
            Message  = $"Processed {accepted + rejected} pings."
        };
    }

    public override async Task<IngestResponse> StreamPings(
        IAsyncStreamReader<TelemetryPing> requestStream,
        ServerCallContext context)
    {
        var tenantId = GetTenantId(context);
        var accepted = 0;
        var rejected = 0;

        await foreach (var ping in requestStream.ReadAllAsync(context.CancellationToken))
        {
            try
            {
                await IngestSinglePingAsync(ping, tenantId, context.CancellationToken);
                accepted++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Rejected streamed ping for vehicle {VehicleId}", ping.VehicleId);
                rejected++;
            }
        }

        return new IngestResponse
        {
            Accepted = accepted,
            Rejected = rejected,
            Message  = $"Stream processed: {accepted} accepted, {rejected} rejected."
        };
    }

    private async Task IngestSinglePingAsync(TelemetryPing ping, Guid tenantId, CancellationToken ct)
    {
        if (!Guid.TryParse(ping.VehicleId, out var vehicleId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid vehicle_id: {ping.VehicleId}"));

        var cmd = new IngestTelemetryCommand(
            VehicleId:      vehicleId,
            TenantId:       tenantId,
            DriverId:       null, // se resuelve desde Fleet service en siguientes iteraciones
            TimestampUnixMs: ping.TimestampMs,
            Latitude:       ping.Location.Lat,
            Longitude:      ping.Location.Lng,
            SpeedKmh:       ping.SpeedKmh,
            HeadingDeg:     ping.HeadingDeg,
            FuelPct:        ping.FuelPct,
            EngineOn:       ping.EngineOn,
            Obd2Codes:      ping.Obd2Codes.Count > 0 ? ping.Obd2Codes : null,
            OdometerKm:     ping.OdometerKm > 0 ? ping.OdometerKm : null);

        await _mediator.Send(cmd, ct);
    }

    private static Guid GetTenantId(ServerCallContext context)
    {
        var claim = context.GetHttpContext().User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(claim, out var tenantId))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "tenant_id claim missing."));
        return tenantId;
    }
}
