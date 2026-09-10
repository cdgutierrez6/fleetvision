using FleetVision.Billing.Application.Common.Interfaces;
using FleetVision.Billing.Application.Subscriptions.Commands.CancelSubscription;
using FleetVision.Billing.Domain.Entities;
using FleetVision.Billing.Domain.Enums;
using FleetVision.Billing.Domain.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FleetVision.Billing.Application.Tests;

public sealed class CancelSubscriptionCommandHandlerTests
{
    private readonly IStripeService _stripe = Substitute.For<IStripeService>();
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string StripeCustomerId = "cus_test";
    private const string StripeSubscriptionId = "sub_test";

    private CancelSubscriptionCommandHandler CreateHandler(IBillingDbContext db)
        => new(db, _stripe, NullLogger<CancelSubscriptionCommandHandler>.Instance);

    // ─── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ActiveSubscription_SetsCancelAtPeriodEnd_AndCallsStripe()
    {
        var db = BuildInMemoryDb();
        var sub = Subscription.Create(TenantId, StripeCustomerId);
        sub.Activate(StripeSubscriptionId, PlanTier.Starter, null, null);
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        await CreateHandler(db).Handle(new CancelSubscriptionCommand(TenantId), default);

        await _stripe.Received(1).SetCancelAtPeriodEndAsync(
            StripeSubscriptionId, true, Arg.Any<CancellationToken>());
        var updated = await db.Subscriptions.SingleAsync();
        updated.CancelAtPeriodEnd.Should().BeTrue();
    }

    // ─── Not found ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NoSubscription_ThrowsSubscriptionNotFound_AndNeverCallsStripe()
    {
        var db = BuildInMemoryDb();

        var act = async () => await CreateHandler(db).Handle(
            new CancelSubscriptionCommand(TenantId), default);

        await act.Should().ThrowAsync<SubscriptionNotFoundException>();
        await _stripe.DidNotReceive().SetCancelAtPeriodEndAsync(
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    // ─── No active Stripe subscription (record exists but no StripeSubscriptionId) ─

    [Fact]
    public async Task Handle_SubscriptionWithoutStripeSubscriptionId_ThrowsNoActiveStripeSubscription()
    {
        var db = BuildInMemoryDb();
        // Subscription.Create leaves StripeSubscriptionId null (Free/never checked out)
        db.Subscriptions.Add(Subscription.Create(TenantId, StripeCustomerId));
        await db.SaveChangesAsync();

        var act = async () => await CreateHandler(db).Handle(
            new CancelSubscriptionCommand(TenantId), default);

        await act.Should().ThrowAsync<NoActiveStripeSubscriptionException>();
        await _stripe.DidNotReceive().SetCancelAtPeriodEndAsync(
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    // ─── Already canceled (idempotency guard) ────────────────────────────────

    [Fact]
    public async Task Handle_AlreadySetToCancel_ThrowsSubscriptionAlreadyCanceled()
    {
        var db = BuildInMemoryDb();
        var sub = Subscription.Create(TenantId, StripeCustomerId);
        sub.Activate(StripeSubscriptionId, PlanTier.Starter, null, null);
        sub.SetCancelAtPeriodEnd(true);
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var act = async () => await CreateHandler(db).Handle(
            new CancelSubscriptionCommand(TenantId), default);

        await act.Should().ThrowAsync<SubscriptionAlreadyCanceledException>();
        await _stripe.DidNotReceive().SetCancelAtPeriodEndAsync(
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    private static TestBillingDbContext BuildInMemoryDb()
        => new(new DbContextOptionsBuilder<TestBillingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
