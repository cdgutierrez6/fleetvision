using FleetVision.TenantManagement.Application.TenantProfiles.Commands.SetPlanByBilling;
using FleetVision.TenantManagement.Domain.Entities;
using FleetVision.TenantManagement.Domain.Enums;
using FleetVision.TenantManagement.Domain.Exceptions;
using FleetVision.TenantManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FleetVision.TenantManagement.Application.Tests.TenantProfiles;

public sealed class SetPlanByBillingHandlerTests : IDisposable
{
    private readonly TenantManagementDbContext _db;
    private readonly SetPlanByBillingCommandHandler _handler;

    public SetPlanByBillingHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TenantManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db      = new TenantManagementDbContext(options);
        _handler = new SetPlanByBillingCommandHandler(
            _db, NullLogger<SetPlanByBillingCommandHandler>.Instance);
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
    public async Task Handle_Upgrade_ShouldPersistNewPlanAndLimits()
    {
        var tenantId = await SeedProfileAsync(PlanTier.Free);

        await _handler.Handle(new SetPlanByBillingCommand(tenantId, PlanTier.Enterprise), default);

        var saved = await _db.TenantProfiles.FirstAsync(t => t.TenantId == tenantId);
        saved.Plan.Should().Be(PlanTier.Enterprise);
        saved.MaxVehicles.Should().Be(1000);
        saved.MaxUsers.Should().Be(int.MaxValue);
    }

    [Fact]
    public async Task Handle_Downgrade_ShouldBeAllowed_BypassesUpgradeOnlyRule()
    {
        var tenantId = await SeedProfileAsync(PlanTier.Enterprise);

        // Billing-driven (Stripe) downgrade must succeed where UpdateTenantPlan would reject it.
        await _handler.Handle(new SetPlanByBillingCommand(tenantId, PlanTier.Free), default);

        var saved = await _db.TenantProfiles.FirstAsync(t => t.TenantId == tenantId);
        saved.Plan.Should().Be(PlanTier.Free);
        saved.MaxVehicles.Should().Be(3);
        saved.MaxUsers.Should().Be(5);
    }

    [Fact]
    public async Task Handle_SamePlan_ShouldKeepLimits()
    {
        var tenantId = await SeedProfileAsync(PlanTier.Starter);

        await _handler.Handle(new SetPlanByBillingCommand(tenantId, PlanTier.Starter), default);

        var saved = await _db.TenantProfiles.FirstAsync(t => t.TenantId == tenantId);
        saved.Plan.Should().Be(PlanTier.Starter);
        saved.MaxVehicles.Should().Be(25);
    }

    [Fact]
    public async Task Handle_NonExistentTenant_ShouldThrow()
    {
        var act = async () => await _handler.Handle(
            new SetPlanByBillingCommand(Guid.NewGuid(), PlanTier.Professional), default);

        await act.Should().ThrowAsync<TenantProfileNotFoundException>();
    }

    public void Dispose() => _db.Dispose();
}
