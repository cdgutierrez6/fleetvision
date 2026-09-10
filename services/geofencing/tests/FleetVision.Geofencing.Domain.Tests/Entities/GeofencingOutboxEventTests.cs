using System.Text;
using FleetVision.Geofencing.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace FleetVision.Geofencing.Domain.Tests.Entities;

public sealed class GeofencingOutboxEventTests
{
    [Fact]
    public void Create_ShouldPopulateFieldsAndDefaults()
    {
        var payload = Encoding.UTF8.GetBytes("{\"event\":\"violation\"}");

        var evt = GeofencingOutboxEvent.Create(
            topic: "geofencing.violations",
            partitionKey: "tenant-123",
            payload: payload);

        evt.Id.Should().NotBeEmpty();
        evt.Topic.Should().Be("geofencing.violations");
        evt.PartitionKey.Should().Be("tenant-123");
        evt.Payload.Should().BeEquivalentTo(payload);
        evt.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_ShouldLeavePublishingStateUnset()
    {
        var evt = GeofencingOutboxEvent.Create("topic", "key", Array.Empty<byte>());

        evt.PublishedAt.Should().BeNull();
        evt.RetryCount.Should().Be(0);
        evt.LastError.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldGenerateUniqueIds()
    {
        var a = GeofencingOutboxEvent.Create("t", "k", Array.Empty<byte>());
        var b = GeofencingOutboxEvent.Create("t", "k", Array.Empty<byte>());

        a.Id.Should().NotBe(b.Id);
    }

    [Fact]
    public void Create_ShouldPreservePayloadBytes()
    {
        var payload = new byte[] { 0x01, 0x02, 0x03, 0xFF };

        var evt = GeofencingOutboxEvent.Create("t", "k", payload);

        evt.Payload.Should().Equal(payload);
    }
}
