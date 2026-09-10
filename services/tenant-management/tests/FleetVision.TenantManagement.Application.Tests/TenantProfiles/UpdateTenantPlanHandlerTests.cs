using FleetVision.TenantManagement.Application.TenantProfiles.Commands.UpdateTenantPlan;
using FleetVision.TenantManagement.Domain.Entities;
using FleetVision.TenantManagement.Domain.Enums;
using FleetVision.TenantManagement.Domain.Exceptions;
using FleetVision.TenantManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FleetVision.TenantManagement.Application.Tests.TenantProfiles;

public sealed class UpdateTenantPlanHandlerTests : IDisposable
{
    private readonly TenantManagementDbContext _db;
    private readonly UpdateTenantPlanCommandHandler _handler;

    public UpdateTenantPlanHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TenantManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db      = new TenantManagementDbContext(options);
        _handler = new UpdateTenantPlanCommandHandler(
            _db, NullLogger<UpdateTenantPlanCommandHandler>.Instance);
    }

    private async Task<Guid> SeedProfileAsync(PlanTier plan)
    {
        var tenantId = Guid.NewGuid();
        var profile  = TenantProfile.Create(tenantId, "Acme Corp", "acme-corp", "billing@acme.com", plan);
        _db.TenantProfiles.Add(profile);
        await _db.SaveChangesAsync();
        return tenantId;
    }

    [Fact]
    public async Task Handle_UpgradePlan_ShouldReturnUpdatedDtoWithNewLimits()
    {
        var tenantId = await SeedProfileAsync(PlanTier.Free);

        var result = await _handler.Handle(
            new UpdateTenantPlanCommand(tenantId, PlanTier.Professional), default);

        result.Plan.Should().Be("Professional");
        result.MaxVehicles.Should().Be(100);
        result.MaxUsers.Should().Be(100);
    }

    [Fact]
    public async Task Handle_UpgradePlan_ShouldPersistChange()
    {
        var tenantId = await SeedProfileAsync(PlanTier.Starter);

        await _handler.Handle(new UpdateTenantPlanCommand(tenantId, PlanTier.Enterprise), default);

        var saved = await _db.TenantProfiles.FirstAsync(t => t.TenantId == tenantId);
        saved.Plan.Should().Be(PlanTier.Enterprise);
        saved.MaxVehicles.Should().Be(1000);
        saved.MaxUsers.Should().Be(int.MaxValue);
    }

    [Fact]
    public async Task Handle_Downgrade_ShouldThrowPlanDowngradeNotAllowed()
    {
        var tenantId = await SeedProfileAsync(PlanTier.Professional);

        var act = async () => await _handler.Handle(
            new UpdateTenantPlanCommand(tenantId, PlanTier.Free), default);

        await act.Should().ThrowAsync<PlanDowngradeNotAllowedException>();
    }

    [Fact]
    public async Task Handle_SamePlan_ShouldSucceedAndKeepLimits()
    {
        var tenantId = await SeedProfileAsync(PlanTier.Starter);

        var result = await _handler.Handle(
            new UpdateTenantPlanCommand(tenantId, PlanTier.Starter), default);

        result.Plan.Should().Be("Starter");
        result.MaxVehicles.Should().Be(25);
    }

    [Fact]
    public async Task Handle_NonExistentTenant_ShouldThrow()
    {
        var act = async () => await _handler.Handle(
            new UpdateTenantPlanCommand(Guid.NewGuid(), PlanTier.Starter), default);

        await act.Should().ThrowAsync<TenantProfileNotFoundException>();
    }

    public void Dispose() => _db.Dispose();
}
