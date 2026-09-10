using FleetVision.Billing.Application.Common.Interfaces;
using FleetVision.Billing.Application.Subscriptions.Commands.CreateCheckoutSession;
using FleetVision.Billing.Domain.Entities;
using FleetVision.Billing.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FleetVision.Billing.Application.Tests;

public sealed class CreateCheckoutSessionCommandHandlerTests
{
    private readonly IStripeService _stripe = Substitute.For<IStripeService>();
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string BillingEmail = "billing@tenant.test";
    private const string ExistingCustomerId = "cus_existing";
    private const string NewCustomerId = "cus_new";
    private const string SessionUrl = "https://checkout.stripe.test/session";

    private CreateCheckoutSessionCommandHandler CreateHandler(IBillingDbContext db)
        => new(db, _stripe, NullLogger<CreateCheckoutSessionCommandHandler>.Instance);

    // ─── First-time checkout: no existing subscription record ─────────────────

    [Fact]
    public async Task Handle_NoExistingSubscription_CreatesCustomer_PersistsSubscription_ReturnsUrl()
    {
        var db = BuildInMemoryDb();
        _stripe.GetOrCreateCustomerAsync(TenantId, BillingEmail, Arg.Any<CancellationToken>())
            .Returns(NewCustomerId);
        _stripe.CreateCheckoutSessionAsync(
            TenantId, NewCustomerId, PlanTier.Professional, Arg.Any<CancellationToken>())
            .Returns(SessionUrl);

        var result = await CreateHandler(db).Handle(
            new CreateCheckoutSessionCommand(TenantId, BillingEmail, PlanTier.Professional), default);

        result.Should().Be(SessionUrl);
        // A new Free/Active subscription shell is persisted with the created customer id
        var sub = await db.Subscriptions.SingleAsync();
        sub.TenantId.Should().Be(TenantId);
        sub.StripeCustomerId.Should().Be(NewCustomerId);
        await _stripe.Received(1).GetOrCreateCustomerAsync(
            TenantId, BillingEmail, Arg.Any<CancellationToken>());
    }

    // ─── Returning tenant: existing subscription reuses its customer id ───────

    [Fact]
    public async Task Handle_ExistingSubscription_ReusesCustomerId_DoesNotCreateCustomer()
    {
        var db = BuildInMemoryDb();
        db.Subscriptions.Add(Subscription.Create(TenantId, ExistingCustomerId));
        await db.SaveChangesAsync();

        _stripe.CreateCheckoutSessionAsync(
            TenantId, ExistingCustomerId, PlanTier.Starter, Arg.Any<CancellationToken>())
            .Returns(SessionUrl);

        var result = await CreateHandler(db).Handle(
            new CreateCheckoutSessionCommand(TenantId, BillingEmail, PlanTier.Starter), default);

        result.Should().Be(SessionUrl);
        await _stripe.DidNotReceive().GetOrCreateCustomerAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _stripe.Received(1).CreateCheckoutSessionAsync(
            TenantId, ExistingCustomerId, PlanTier.Starter, Arg.Any<CancellationToken>());
        // No duplicate subscription rows created for a returning tenant
        (await db.Subscriptions.CountAsync()).Should().Be(1);
    }

    private static TestBillingDbContext BuildInMemoryDb()
        => new(new DbContextOptionsBuilder<TestBillingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
