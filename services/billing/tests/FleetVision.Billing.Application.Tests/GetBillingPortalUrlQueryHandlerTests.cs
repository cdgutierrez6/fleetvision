using FleetVision.Billing.Application.Common.Interfaces;
using FleetVision.Billing.Application.Subscriptions.Queries.GetBillingPortalUrl;
using FleetVision.Billing.Domain.Entities;
using FleetVision.Billing.Domain.Enums;
using FleetVision.Billing.Domain.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace FleetVision.Billing.Application.Tests;

public sealed class GetBillingPortalUrlQueryHandlerTests
{
    private readonly IStripeService _stripe = Substitute.For<IStripeService>();
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string StripeCustomerId = "cus_test";
    private const string StripeSubscriptionId = "sub_test";
    private const string ReturnUrl = "https://app.fleetvision.test/billing";
    private const string PortalUrl = "https://portal.stripe.test/session";

    private GetBillingPortalUrlQueryHandler CreateHandler(IBillingDbContext db)
        => new(db, _stripe);

    // ─── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ActiveSubscription_ReturnsPortalUrl()
    {
        var db = BuildInMemoryDb();
        var sub = Subscription.Create(TenantId, StripeCustomerId);
        sub.Activate(StripeSubscriptionId, PlanTier.Starter, null, null);
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        _stripe.CreateCustomerPortalSessionAsync(
            StripeCustomerId, ReturnUrl, Arg.Any<CancellationToken>())
            .Returns(PortalUrl);

        var result = await CreateHandler(db).Handle(
            new GetBillingPortalUrlQuery(TenantId, ReturnUrl), default);

        result.Should().Be(PortalUrl);
        await _stripe.Received(1).CreateCustomerPortalSessionAsync(
            StripeCustomerId, ReturnUrl, Arg.Any<CancellationToken>());
    }

    // ─── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NoSubscription_ThrowsSubscriptionNotFound()
    {
        var db = BuildInMemoryDb();

        var act = async () => await CreateHandler(db).Handle(
            new GetBillingPortalUrlQuery(TenantId, ReturnUrl), default);

        await act.Should().ThrowAsync<SubscriptionNotFoundException>();
        await _stripe.DidNotReceive().CreateCustomerPortalSessionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ─── No active Stripe subscription ───────────────────────────────────────

    [Fact]
    public async Task Handle_SubscriptionWithoutStripeSubscriptionId_ThrowsNoActiveStripeSubscription()
    {
        var db = BuildInMemoryDb();
        // Free tenant: record exists but never completed checkout (StripeSubscriptionId null)
        db.Subscriptions.Add(Subscription.Create(TenantId, StripeCustomerId));
        await db.SaveChangesAsync();

        var act = async () => await CreateHandler(db).Handle(
            new GetBillingPortalUrlQuery(TenantId, ReturnUrl), default);

        await act.Should().ThrowAsync<NoActiveStripeSubscriptionException>();
        await _stripe.DidNotReceive().CreateCustomerPortalSessionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static TestBillingDbContext BuildInMemoryDb()
        => new(new DbContextOptionsBuilder<TestBillingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
