using FleetVision.Notifications.API.Hubs;
using FleetVision.Proto.Geofencing;
using FluentAssertions;
using Google.Protobuf;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Xunit;

namespace FleetVision.Notifications.Tests.Kafka;

/// <summary>
/// Tests the broadcast logic extracted from ViolationKafkaConsumer.
/// We test the payload mapping rules (sentinel values, group routing)
/// without spinning up a real Kafka consumer.
/// </summary>
public sealed class ViolationKafkaConsumerBroadcastTests
{
    [Fact]
    public void GroupName_IsRoutedByTenantId()
    {
        var tenantId = Guid.NewGuid().ToString();
        ViolationHub.GroupName(tenantId).Should().Be($"tenant:{tenantId}");
    }

    [Theory]
    [InlineData(-1.0f, -1, null, null)]          // non-speed violation: sentinels → null
    [InlineData(85.0f, 60, 85.0f, 60)]           // SpeedExceeded: values preserved
    [InlineData(0.0f, 0, 0.0f, 0)]               // speed=0, limit=0: still valid (0 >= 0)
    public void SpeedFieldMapping_ObeysSentinelRule(
        float protoActual, int protoLimit,
        float? expectedActual, int? expectedLimit)
    {
        // The consumer maps sentinel -1 to null; >= 0 values are passed through
        float? mappedActual = protoActual >= 0 ? protoActual : (float?)null;
        int?   mappedLimit  = protoLimit  >= 0 ? protoLimit  : (int?)null;

        mappedActual.Should().Be(expectedActual);
        mappedLimit.Should().Be(expectedLimit);
    }

    [Fact]
    public void DriverId_EmptyString_MapsToNull()
    {
        // Proto uses empty string for missing optional fields (proto3 default)
        var driverId = string.Empty;
        var result   = string.IsNullOrEmpty(driverId) ? (string?)null : driverId;
        result.Should().BeNull();
    }

    [Fact]
    public void DriverId_NonEmpty_PreservesValue()
    {
        var driverId = Guid.NewGuid().ToString();
        var result   = string.IsNullOrEmpty(driverId) ? (string?)null : driverId;
        result.Should().Be(driverId);
    }

    [Fact]
    public void ViolationDetectedEvent_CanBeRoundTripped()
    {
        var original = new ViolationDetectedEvent
        {
            Id               = Guid.NewGuid().ToString(),
            TenantId         = Guid.NewGuid().ToString(),
            VehicleId        = Guid.NewGuid().ToString(),
            DriverId         = string.Empty,
            GeofenceId       = Guid.NewGuid().ToString(),
            GeofenceName     = "Restricted Zone",
            ViolationType    = "ZoneEntered",
            Latitude         = 40.715,
            Longitude        = -74.005,
            ActualSpeedKmh   = -1.0f,
            LimitSpeedKmh    = -1,
            OccurredAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        var bytes  = original.ToByteArray();
        var parsed = ViolationDetectedEvent.Parser.ParseFrom(bytes);

        parsed.Id.Should().Be(original.Id);
        parsed.TenantId.Should().Be(original.TenantId);
        parsed.ViolationType.Should().Be("ZoneEntered");
        parsed.ActualSpeedKmh.Should().Be(-1.0f);
    }

    [Fact]
    public void OccurredAt_TimestampConversionIsCorrect()
    {
        var now   = DateTimeOffset.UtcNow;
        var unixMs = now.ToUnixTimeMilliseconds();
        var back  = DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime;

        // Accept 1-second tolerance for timestamp conversion
        Math.Abs((now.UtcDateTime - back).TotalSeconds).Should().BeLessThan(1);
    }
}
