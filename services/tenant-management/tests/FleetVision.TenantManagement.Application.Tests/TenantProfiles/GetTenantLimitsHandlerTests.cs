using FleetVision.TenantManagement.Application.TenantProfiles.Commands.CreateTenantProfile;
using FleetVision.TenantManagement.Application.TenantProfiles.Queries.GetTenantLimits;
using FleetVision.TenantManagement.Domain.Entities;
using FleetVision.TenantManagement.Domain.Enums;
using FleetVision.TenantManagement.Domain.Exceptions;
using FleetVision.TenantManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FleetVision.TenantManagement.Application.Tests.TenantProfiles;

public sealed class GetTenantLimitsHandlerTests : IDisposable
{
    private readonly TenantManagementDbContext _db;
    private readonly GetTenantLimitsQueryHandler _handler;

    public GetTenantLimitsHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TenantManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db      = new TenantManagementDbContext(options);
        _handler = new GetTenantLimitsQueryHandler(_db);
    }

    private async Task<Guid> SeedProfileAsync(PlanTier plan)
    {
        var tenantId = Guid.NewGuid();
        var profile  = TenantProfile.Create(tenantId, "Test Corp", "test-corp", "b@test.com", plan);
        _db.TenantProfiles.Add(profile);
        await _db.SaveChangesAsync();
        return tenantId;
    }

    [Theory]
    [InlineData(PlanTier.Free,         3,    5)]
    [InlineData(PlanTier.Starter,     25,   25)]
    [InlineData(PlanTier.Professional,100,  100)]
    [InlineData(PlanTier.Enterprise,  1000, int.MaxValue)]
    public async Task Handle_ShouldReturnCorrectLimitsPerPlan(
        PlanTier plan, int expectedVehicles, int expectedUsers)
    {
        var tenantId = await SeedProfileAsync(plan);

        var result = await _handler.Handle(new GetTenantLimitsQuery(tenantId), default);

        result.TenantId.Should().Be(tenantId);
        result.Plan.Should().Be(plan.ToString());
        result.MaxVehicles.Should().Be(expectedVehicles);
        result.MaxUsers.Should().Be(expectedUsers);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithNonExistentTenant_ShouldThrow()
    {
        var act = async () => await _handler.Handle(
            new GetTenantLimitsQuery(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<TenantProfileNotFoundException>();
    }

    [Fact]
    public async Task Handle_DeactivatedTenant_ShouldReturnIsActiveFalse()
    {
        var tenantId = await SeedProfileAsync(PlanTier.Starter);
        var profile  = await _db.TenantProfiles.FirstAsync();
        profile.Deactivate();
        await _db.SaveChangesAsync();

        var result = await _handler.Handle(new GetTenantLimitsQuery(tenantId), default);

        result.IsActive.Should().BeFalse();
    }

    public void Dispose() => _db.Dispose();
}
