using FleetVision.TenantManagement.Application.TenantProfiles.Queries.GetTenantProfile;
using FleetVision.TenantManagement.Domain.Entities;
using FleetVision.TenantManagement.Domain.Enums;
using FleetVision.TenantManagement.Domain.Exceptions;
using FleetVision.TenantManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FleetVision.TenantManagement.Application.Tests.TenantProfiles;

public sealed class GetTenantProfileHandlerTests : IDisposable
{
    private readonly TenantManagementDbContext _db;
    private readonly GetTenantProfileQueryHandler _handler;

    public GetTenantProfileHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TenantManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db      = new TenantManagementDbContext(options);
        _handler = new GetTenantProfileQueryHandler(_db);
    }

    private async Task<Guid> SeedProfileAsync(PlanTier plan = PlanTier.Professional)
    {
        var tenantId = Guid.NewGuid();
        var profile  = TenantProfile.Create(tenantId, "Acme Corp", "acme-corp", "billing@acme.com", plan);
        _db.TenantProfiles.Add(profile);
        await _db.SaveChangesAsync();
        return tenantId;
    }

    [Fact]
    public async Task Handle_WithExistingTenant_ShouldReturnMappedDto()
    {
        var tenantId = await SeedProfileAsync();

        var result = await _handler.Handle(new GetTenantProfileQuery(tenantId), default);

        result.TenantId.Should().Be(tenantId);
        result.CompanyName.Should().Be("Acme Corp");
        result.Slug.Should().Be("acme-corp");
        result.BillingEmail.Should().Be("billing@acme.com");
        result.Plan.Should().Be("Professional");
        result.MaxVehicles.Should().Be(100);
        result.MaxUsers.Should().Be(100);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithNonExistentTenant_ShouldThrow()
    {
        var act = async () => await _handler.Handle(
            new GetTenantProfileQuery(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<TenantProfileNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldNotReturnOtherTenantsProfile()
    {
        await SeedProfileAsync();
        var otherId = await SeedProfileAsync(PlanTier.Free);

        var result = await _handler.Handle(new GetTenantProfileQuery(otherId), default);

        result.TenantId.Should().Be(otherId);
        result.Plan.Should().Be("Free");
    }

    public void Dispose() => _db.Dispose();
}
