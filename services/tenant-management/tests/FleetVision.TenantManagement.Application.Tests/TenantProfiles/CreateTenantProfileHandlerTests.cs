using FleetVision.TenantManagement.Application.TenantProfiles.Commands.CreateTenantProfile;
using FleetVision.TenantManagement.Domain.Enums;
using FleetVision.TenantManagement.Domain.Exceptions;
using FleetVision.TenantManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FleetVision.TenantManagement.Application.Tests.TenantProfiles;

public sealed class CreateTenantProfileHandlerTests : IDisposable
{
    private readonly TenantManagementDbContext _db;
    private readonly CreateTenantProfileCommandHandler _handler;

    public CreateTenantProfileHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TenantManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db      = new TenantManagementDbContext(options);
        _handler = new CreateTenantProfileCommandHandler(
            _db, NullLogger<CreateTenantProfileCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnDto()
    {
        var tenantId = Guid.NewGuid();
        var command  = new CreateTenantProfileCommand(
            tenantId, "Acme Corp", "acme-corp", "billing@acme.com");

        var result = await _handler.Handle(command, default);

        result.TenantId.Should().Be(tenantId);
        result.CompanyName.Should().Be("Acme Corp");
        result.Plan.Should().Be("Free");
        result.MaxVehicles.Should().Be(3);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldPersistProfile()
    {
        var tenantId = Guid.NewGuid();
        var command  = new CreateTenantProfileCommand(
            tenantId, "Acme Corp", "acme-corp", "billing@acme.com", PlanTier.Starter);

        await _handler.Handle(command, default);

        var saved = await _db.TenantProfiles.FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.TenantId.Should().Be(tenantId);
        saved.Plan.Should().Be(PlanTier.Starter);
        saved.MaxVehicles.Should().Be(25);
    }

    [Fact]
    public async Task Handle_WithDuplicateTenantId_ShouldThrow()
    {
        var tenantId = Guid.NewGuid();
        var command  = new CreateTenantProfileCommand(
            tenantId, "Acme Corp", "acme-corp", "billing@acme.com");

        await _handler.Handle(command, default);

        var act = async () => await _handler.Handle(
            new CreateTenantProfileCommand(tenantId, "Other Corp", "other", "other@b.com"), default);

        await act.Should().ThrowAsync<TenantProfileAlreadyExistsException>();
    }

    [Fact]
    public async Task Handle_ShouldNormalizeSlugToLowercase()
    {
        var command = new CreateTenantProfileCommand(
            Guid.NewGuid(), "Acme Corp", "ACME-CORP", "billing@acme.com");

        var result = await _handler.Handle(command, default);

        result.Slug.Should().Be("acme-corp");
    }

    [Fact]
    public async Task Handle_ShouldSetCorrectLimitsForEnterprisePlan()
    {
        var command = new CreateTenantProfileCommand(
            Guid.NewGuid(), "Big Corp", "big-corp", "billing@big.com", PlanTier.Enterprise);

        var result = await _handler.Handle(command, default);

        result.MaxVehicles.Should().Be(1000);
        result.MaxUsers.Should().Be(int.MaxValue);
    }

    public void Dispose() => _db.Dispose();
}
