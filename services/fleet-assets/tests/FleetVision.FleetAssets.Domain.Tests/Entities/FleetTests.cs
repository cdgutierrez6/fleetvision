using FleetVision.FleetAssets.Domain.Entities;
using FleetVision.FleetAssets.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace FleetVision.FleetAssets.Domain.Tests.Entities;

public sealed class FleetTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateFleet()
    {
        var tenantId = Guid.NewGuid();

        var fleet = Fleet.Create(tenantId, "North Fleet", "Trucks for northern routes");

        fleet.Id.Should().NotBeEmpty();
        fleet.TenantId.Should().Be(tenantId);
        fleet.Name.Should().Be("North Fleet");
        fleet.Description.Should().Be("Trucks for northern routes");
        fleet.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrow()
    {
        var act = () => Fleet.Create(Guid.Empty, "Fleet A");
        act.Should().Throw<ArgumentException>().WithMessage("*TenantId*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_ShouldThrow(string name)
    {
        var act = () => Fleet.Create(Guid.NewGuid(), name);
        act.Should().Throw<ArgumentException>().WithMessage("*Name*");
    }

    [Fact]
    public void Create_WithNameOver100Chars_ShouldThrow()
    {
        var name = new string('x', 101);
        var act  = () => Fleet.Create(Guid.NewGuid(), name);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldTrimName()
    {
        var fleet = Fleet.Create(Guid.NewGuid(), "  North Fleet  ");
        fleet.Name.Should().Be("North Fleet");
    }

    [Fact]
    public void Update_ShouldChangeNameAndDescription()
    {
        var fleet = Fleet.Create(Guid.NewGuid(), "Old Name", "Old desc");
        var before = fleet.UpdatedAt;

        fleet.Update("New Name", "New desc");

        fleet.Name.Should().Be("New Name");
        fleet.Description.Should().Be("New desc");
        fleet.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Update_WithNullDescription_ShouldClearDescription()
    {
        var fleet = Fleet.Create(Guid.NewGuid(), "Fleet", "Description");
        fleet.Update("Fleet", null);
        fleet.Description.Should().BeNull();
    }
}
