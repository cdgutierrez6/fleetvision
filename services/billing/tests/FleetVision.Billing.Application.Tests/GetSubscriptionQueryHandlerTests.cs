using FleetVision.Billing.Application.Subscriptions.Queries.GetSubscription;
using FleetVision.Billing.Domain.Entities;
using FleetVision.Billing.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.Billing.Application.Tests;

public sealed class GetSubscriptionQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string StripeCustomerId = "cus_test";
    private const string StripeSubscriptionId = "sub_test";

    // ─── Existing subscription → mapped DTO ──────────────────────────────────

    [Fact]
    public async Task Handle_ExistingSubscription_ReturnsMappedDto()
    {
        var db = BuildInMemoryDb();
        var periodEnd = DateTime.UtcNow.AddDays(30);
        var sub = Subscription.Create(TenantId, StripeCustomerId);
        sub.Activate(StripeSubscriptionId, PlanTier.Professional, DateTime.UtcNow, periodEnd);
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var dto = await new GetSubscriptionQueryHandler(db).Handle(
            new GetSubscriptionQuery(TenantId), default);

        dto.TenantId.Should().Be(TenantId);
        dto.Plan.Should().Be(PlanTier.Professional.ToString());
        dto.Status.Should().Be(SubscriptionStatus.Active.ToString());
        dto.CurrentPeriodEnd.Should().Be(periodEnd);
        dto.CancelAtPeriodEnd.Should().BeFalse();
        dto.StripeSubscriptionId.Should().Be(StripeSubscriptionId);
    }

    // ─── No record → implicit Free/Active default ────────────────────────────

    [Fact]
    public async Task Handle_NoSubscription_ReturnsFreeActiveDefault()
    {
        var db = BuildInMemoryDb();

        var dto = await new GetSubscriptionQueryHandler(db).Handle(
            new GetSubscriptionQuery(TenantId), default);

        dto.TenantId.Should().Be(TenantId);
        dto.Plan.Should().Be(PlanTier.Free.ToString());
        dto.Status.Should().Be(SubscriptionStatus.Active.ToString());
        dto.CurrentPeriodEnd.Should().BeNull();
        dto.CancelAtPeriodEnd.Should().BeFalse();
        dto.StripeSubscriptionId.Should().BeNull();
    }

    // ─── Cross-tenant isolation ──────────────────────────────────────────────
    // Defense-in-depth at the handler level: the `WHERE TenantId ==` filter must
    // return only the requesting tenant's record even when another tenant's
    // subscription lives in the same table. This is independent of the Postgres
    // RLS layer (TenantRlsInterceptor), which EF Core InMemory does not enforce —
    // so it verifies the handler's own filtering rather than the database policy.

    [Fact]
    public async Task Handle_AnotherTenantHasSubscription_ReturnsOnlyRequestingTenants()
    {
        var db = BuildInMemoryDb();

        var mine = Subscription.Create(TenantId, StripeCustomerId);
        mine.Activate(StripeSubscriptionId, PlanTier.Professional, null, null);

        var otherTenant = Guid.NewGuid();
        var theirs = Subscription.Create(otherTenant, "cus_other");
        theirs.Activate("sub_other", PlanTier.Starter, null, null);

        db.Subscriptions.AddRange(mine, theirs);
        await db.SaveChangesAsync();

        var dto = await new GetSubscriptionQueryHandler(db).Handle(
            new GetSubscriptionQuery(TenantId), default);

        dto.TenantId.Should().Be(TenantId);
        dto.Plan.Should().Be(PlanTier.Professional.ToString());
        dto.StripeSubscriptionId.Should().Be(StripeSubscriptionId);
        // The other tenant's subscription must never surface
        dto.StripeSubscriptionId.Should().NotBe("sub_other");
    }

    private static TestBillingDbContext BuildInMemoryDb()
        => new(new DbContextOptionsBuilder<TestBillingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
