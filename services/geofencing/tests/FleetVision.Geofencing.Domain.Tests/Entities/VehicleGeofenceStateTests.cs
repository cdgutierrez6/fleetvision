using FleetVision.Geofencing.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace FleetVision.Geofencing.Domain.Tests.Entities;

public sealed class VehicleGeofenceStateTests
{
    [Fact]
    public void Create_WithInsideTrue_ShouldPopulateFields()
    {
        var tenantId   = Guid.NewGuid();
        var vehicleId  = Guid.NewGuid();
        var geofenceId = Guid.NewGuid();

        var state = VehicleGeofenceState.Create(tenantId, vehicleId, geofenceId, isInside: true);

        state.Id.Should().NotBeEmpty();
        state.TenantId.Should().Be(tenantId);
        state.VehicleId.Should().Be(vehicleId);
        state.GeofenceId.Should().Be(geofenceId);
        state.IsInside.Should().BeTrue();
        state.LastEvaluatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithInsideFalse_ShouldSetIsInsideFalse()
    {
        var state = VehicleGeofenceState.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), isInside: false);

        state.IsInside.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldGenerateUniqueIds()
    {
        var a = VehicleGeofenceState.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), true);
        var b = VehicleGeofenceState.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), true);

        a.Id.Should().NotBe(b.Id);
    }

    [Fact]
    public void UpdateState_ToFalse_ShouldFlipIsInside()
    {
        var state = VehicleGeofenceState.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), isInside: true);

        state.UpdateState(isInside: false);

        state.IsInside.Should().BeFalse();
    }

    [Fact]
    public void UpdateState_ToTrue_ShouldFlipIsInside()
    {
        var state = VehicleGeofenceState.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), isInside: false);

        state.UpdateState(isInside: true);

        state.IsInside.Should().BeTrue();
    }

    [Fact]
    public void UpdateState_ShouldAdvanceLastEvaluatedAt()
    {
        var state = VehicleGeofenceState.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), isInside: true);

        state.UpdateState(isInside: false);

        state.LastEvaluatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
