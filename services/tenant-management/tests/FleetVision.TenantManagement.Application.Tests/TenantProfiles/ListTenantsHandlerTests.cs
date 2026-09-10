using FleetVision.TenantManagement.Application.TenantProfiles.Queries.ListTenants;
using FleetVision.TenantManagement.Domain.Entities;
using FleetVision.TenantManagement.Domain.Enums;
using FleetVision.TenantManagement.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FleetVision.TenantManagement.Application.Tests.TenantProfiles;

public sealed class ListTenantsHandlerTests : IDisposable
{
    private readonly TenantManagementDbContext _db;
    private readonly ListTenantsQueryHandler _handler;

    public ListTenantsHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TenantManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db      = new TenantManagementDbContext(options);
        _handler = new ListTenantsQueryHandler(_db);
    }

    private async Task SeedAsync(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var profile = TenantProfile.Create(
                Guid.NewGuid(), $"Corp {i}", $"corp-{i}", $"b{i}@test.com", PlanTier.Free);
            _db.TenantProfiles.Add(profile);
        }
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WithNoTenants_ShouldReturnEmptyPage()
    {
        var result = await _handler.Handle(new ListTenantsQuery(), default);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Handle_ShouldReturnAllTenantsWithinPageSize()
    {
        await SeedAsync(5);

        var result = await _handler.Handle(new ListTenantsQuery(1, 20), default);

        result.Items.Should().HaveCount(5);
        result.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task Handle_ShouldPaginate_SecondPageReturnsRemainder()
    {
        await SeedAsync(15);

        var page2 = await _handler.Handle(new ListTenantsQuery(2, 10), default);

        page2.Items.Should().HaveCount(5);
        page2.TotalCount.Should().Be(15);
        page2.Page.Should().Be(2);
        page2.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task Handle_ShouldOrderByCreatedAtDescending()
    {
        var older = TenantProfile.Create(Guid.NewGuid(), "Older", "older", "o@test.com");
        _db.TenantProfiles.Add(older);
        await _db.SaveChangesAsync();

        await Task.Delay(5);

        var newer = TenantProfile.Create(Guid.NewGuid(), "Newer", "newer", "n@test.com");
        _db.TenantProfiles.Add(newer);
        await _db.SaveChangesAsync();

        var result = await _handler.Handle(new ListTenantsQuery(1, 20), default);

        result.Items.First().CompanyName.Should().Be("Newer");
        result.Items.Last().CompanyName.Should().Be("Older");
    }

    [Fact]
    public async Task Handle_WithPageBelowOne_ShouldClampToFirstPage()
    {
        await SeedAsync(3);

        var result = await _handler.Handle(new ListTenantsQuery(0, 20), default);

        result.Page.Should().Be(1);
        result.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithPageSizeAboveMax_ShouldClampToHundred()
    {
        var result = await _handler.Handle(new ListTenantsQuery(1, 500), default);

        result.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task Handle_WithPageSizeBelowOne_ShouldClampToOne()
    {
        await SeedAsync(3);

        var result = await _handler.Handle(new ListTenantsQuery(1, 0), default);

        result.PageSize.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(3);
    }

    public void Dispose() => _db.Dispose();
}
