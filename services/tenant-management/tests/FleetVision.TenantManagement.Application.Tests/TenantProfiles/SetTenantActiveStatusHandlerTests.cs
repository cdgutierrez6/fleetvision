using FleetVision.TenantManagement.Application.TenantProfiles.Commands.SetTenantActiveStatus;
using FleetVision.TenantManagement.Domain.Entities;
using FleetVision.TenantManagement.Domain.Enums;
using FleetVision.TenantManagement.Domain.Exceptions;
using FleetVision.TenantManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FleetVision.TenantManagement.Application.Tests.TenantProfiles;

public sealed class SetTenantActiveStatusHandlerTests : IDisposable
{
    private readonly TenantManagementDbContext _db;
    private readonly SetTenantActiveStatusCommandHandler _handler;

    public SetTenantActiveStatusHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TenantManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db      = new TenantManagementDbContext(options);
        _handler = new SetTenantActiveStatusCommandHandler(
            _db, NullLogger<SetTenantActiveStatusCommandHandler>.Instance);
    }

    private async Task<Guid> SeedProfileAsync()
    {
        var tenantId = Guid.NewGuid();
        var profile  = TenantProfile.Create(tenantId, "Acme Corp", "acme-corp", "billing@acme.com");
        _db.TenantProfiles.Add(profile);
        await _db.SaveChangesAsync();
        return tenantId;
    }

    [Fact]
    public async Task Handle_Deactivate_ShouldPersistIsActiveFalse()
    {
        var tenantId = await SeedProfileAsync();

        await _handler.Handle(new SetTenantActiveStatusCommand(tenantId, false), default);

        var saved = await _db.TenantProfiles.FirstAsync(t => t.TenantId == tenantId);
        saved.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Activate_ShouldPersistIsActiveTrue()
    {
        var tenantId = await SeedProfileAsync();
        var profile  = await _db.TenantProfiles.FirstAsync(t => t.TenantId == tenantId);
        profile.Deactivate();
        await _db.SaveChangesAsync();

        await _handler.Handle(new SetTenantActiveStatusCommand(tenantId, true), default);

        var saved = await _db.TenantProfiles.FirstAsync(t => t.TenantId == tenantId);
        saved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Reactivate_IsIdempotentForAlreadyActive()
    {
        var tenantId = await SeedProfileAsync();

        await _handler.Handle(new SetTenantActiveStatusCommand(tenantId, true), default);

        var saved = await _db.TenantProfiles.FirstAsync(t => t.TenantId == tenantId);
        saved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NonExistentTenant_ShouldThrow()
    {
        var act = async () => await _handler.Handle(
            new SetTenantActiveStatusCommand(Guid.NewGuid(), false), default);

        await act.Should().ThrowAsync<TenantProfileNotFoundException>();
    }

    public void Dispose() => _db.Dispose();
}
