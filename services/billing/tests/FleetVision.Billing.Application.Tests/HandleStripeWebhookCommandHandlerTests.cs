using FleetVision.Billing.Application.Common.Interfaces;
using FleetVision.Billing.Application.Subscriptions.Commands.HandleStripeWebhook;
using FleetVision.Billing.Domain.Entities;
using FleetVision.Billing.Domain.Enums;
using FleetVision.Billing.Domain.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FleetVision.Billing.Application.Tests;

public sealed class HandleStripeWebhookCommandHandlerTests
{
    private readonly IStripeService _stripe       = Substitute.For<IStripeService>();
    private readonly ITenantManagementClient _tm  = Substitute.For<ITenantManagementClient>();
    private readonly IBillingEventPublisher _pub  = Substitute.For<IBillingEventPublisher>();
    private readonly IConfiguration _config       = BuildConfig();

    private static readonly Guid TenantId         = Guid.NewGuid();
    private const string StripeCustomerId         = "cus_test";
    private const string StripeSubscriptionId     = "sub_test";

    private HandleStripeWebhookCommandHandler CreateHandler(IBillingDbContext db)
        => new(db, _stripe, _tm, _pub, _config, NullLogger<HandleStripeWebhookCommandHandler>.Instance);

    // ─── checkout.session.completed ──────────────────────────────────────────

    [Fact]
    public async Task Handle_CheckoutSessionCompleted_ActivatesSubscription()
    {
        var db = BuildInMemoryDb();

        _stripe.ParseWebhookEvent(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new StripeWebhookEvent
            {
                EventType            = "checkout.session.completed",
                TenantId             = TenantId,
                Plan                 = PlanTier.Starter,
                StripeCustomerId     = StripeCustomerId,
                StripeSubscriptionId = StripeSubscriptionId,
            });

        await CreateHandler(db).Handle(new HandleStripeWebhookCommand("payload", "sig"), default);

        var sub = await db.Subscriptions.SingleAsync();
        sub.Plan.Should().Be(PlanTier.Starter);
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.StripeSubscriptionId.Should().Be(StripeSubscriptionId);
    }

    [Fact]
    public async Task Handle_CheckoutSessionCompleted_PublishesTenantProvisioned_OnFirstActivation()
    {
        var db = BuildInMemoryDb();

        _stripe.ParseWebhookEvent(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new StripeWebhookEvent
            {
                EventType            = "checkout.session.completed",
                TenantId             = TenantId,
                Plan                 = PlanTier.Starter,
                StripeCustomerId     = StripeCustomerId,
                StripeSubscriptionId = StripeSubscriptionId,
            });

        await CreateHandler(db).Handle(new HandleStripeWebhookCommand("payload", "sig"), default);

        _pub.Received(1).EnqueueTenantProvisioned(TenantId, PlanTier.Starter, Arg.Any<string>());
        await _tm.Received(1).UpdateTenantPlanAsync(TenantId, PlanTier.Starter, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CheckoutSessionCompleted_InsertsAuditEntry()
    {
        var db = BuildInMemoryDb();

        _stripe.ParseWebhookEvent(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new StripeWebhookEvent
            {
                EventType            = "checkout.session.completed",
                TenantId             = TenantId,
                Plan                 = PlanTier.Professional,
                StripeCustomerId     = StripeCustomerId,
                StripeSubscriptionId = StripeSubscriptionId,
            });

        await CreateHandler(db).Handle(new HandleStripeWebhookCommand("payload", "sig"), default);

        db.AuditLog.Should().ContainSingle();
        var entry = db.AuditLog[0];
        entry.TenantId.Should().Be(TenantId);
        entry.OldPlan.Should().Be("Free");
        entry.NewPlan.Should().Be("Professional");
        entry.Source.Should().Contain("checkout.session.completed");
        entry.StripeEventId.Should().Be(StripeSubscriptionId);
    }

    // ─── Idempotency guard — HIGH-04 ─────────────────────────────────────────

    [Fact]
    public async Task Handle_CheckoutSessionCompleted_WhenAlreadyActive_SameSubId_SkipsAll()
    {
        // Arrange: subscription already active with the same StripeSubscriptionId
        var db = BuildInMemoryDb();
        var existingSub = Subscription.Create(TenantId, StripeCustomerId);
        existingSub.Activate(StripeSubscriptionId, PlanTier.Starter, null, null);
        db.Subscriptions.Add(existingSub);
        await db.SaveChangesAsync();

        _stripe.ParseWebhookEvent(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new StripeWebhookEvent
            {
                EventType            = "checkout.session.completed",
                TenantId             = TenantId,
                Plan                 = PlanTier.Starter,
                StripeCustomerId     = StripeCustomerId,
                StripeSubscriptionId = StripeSubscriptionId, // same ID → duplicate delivery
            });

        // Act
        await CreateHandler(db).Handle(new HandleStripeWebhookCommand("payload", "sig"), default);

        // Assert: no TenantManagement call, no Kafka event, no audit entry
        await _tm.DidNotReceive().UpdateTenantPlanAsync(
            Arg.Any<Guid>(), Arg.Any<PlanTier>(), Arg.Any<CancellationToken>());
        _pub.DidNotReceive().EnqueueTenantProvisioned(
            Arg.Any<Guid>(), Arg.Any<PlanTier>(), Arg.Any<string>());
        _pub.DidNotReceive().EnqueueSubscriptionChanged(
            Arg.Any<Guid>(), Arg.Any<PlanTier>(), Arg.Any<PlanTier>(), Arg.Any<SubscriptionStatus>());
        db.AuditLog.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_CheckoutSessionCompleted_WhenSameSubId_DifferentActivePlan_Processes()
    {
        // Different StripeSubscriptionId means it's a genuine upgrade, not a duplicate
        var db = BuildInMemoryDb();
        var existingSub = Subscription.Create(TenantId, StripeCustomerId);
        existingSub.Activate("sub_old", PlanTier.Starter, null, null);
        db.Subscriptions.Add(existingSub);
        await db.SaveChangesAsync();

        _stripe.ParseWebhookEvent(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new StripeWebhookEvent
            {
                EventType            = "checkout.session.completed",
                TenantId             = TenantId,
                Plan                 = PlanTier.Professional,
                StripeCustomerId     = StripeCustomerId,
                StripeSubscriptionId = "sub_new", // different sub ID → upgrade
            });

        await CreateHandler(db).Handle(new HandleStripeWebhookCommand("payload", "sig"), default);

        var sub = await db.Subscriptions.SingleAsync();
        sub.Plan.Should().Be(PlanTier.Professional);
        db.AuditLog.Should().ContainSingle();
    }

    // ─── customer.subscription.deleted ───────────────────────────────────────

    [Fact]
    public async Task Handle_SubscriptionDeleted_DowngradesToFree()
    {
        var db = BuildInMemoryDb();
        var sub = Subscription.Create(TenantId, StripeCustomerId);
        sub.Activate(StripeSubscriptionId, PlanTier.Professional, null, null);
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        _stripe.ParseWebhookEvent(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new StripeWebhookEvent
            {
                EventType            = "customer.subscription.deleted",
                StripeSubscriptionId = StripeSubscriptionId,
            });

        await CreateHandler(db).Handle(new HandleStripeWebhookCommand("payload", "sig"), default);

        var updated = await db.Subscriptions.SingleAsync();
        updated.Plan.Should().Be(PlanTier.Free);
        updated.Status.Should().Be(SubscriptionStatus.Canceled);
        await _tm.Received(1).UpdateTenantPlanAsync(TenantId, PlanTier.Free, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SubscriptionDeleted_InsertsAuditEntry_WithFreeNewPlan()
    {
        var db = BuildInMemoryDb();
        var sub = Subscription.Create(TenantId, StripeCustomerId);
        sub.Activate(StripeSubscriptionId, PlanTier.Enterprise, null, null);
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        _stripe.ParseWebhookEvent(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new StripeWebhookEvent
            {
                EventType            = "customer.subscription.deleted",
                StripeSubscriptionId = StripeSubscriptionId,
            });

        await CreateHandler(db).Handle(new HandleStripeWebhookCommand("payload", "sig"), default);

        db.AuditLog.Should().ContainSingle();
        var entry = db.AuditLog[0];
        entry.TenantId.Should().Be(TenantId);
        entry.OldPlan.Should().Be("Enterprise");
        entry.NewPlan.Should().Be("Free");
        entry.Source.Should().Contain("subscription.deleted");
    }

    // ─── customer.subscription.updated ───────────────────────────────────────

    [Fact]
    public async Task Handle_SubscriptionUpdated_InsertsAuditEntry_WhenPlanChanges()
    {
        var db = BuildInMemoryDb();
        var sub = Subscription.Create(TenantId, StripeCustomerId);
        sub.Activate(StripeSubscriptionId, PlanTier.Starter, null, null);
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        _stripe.ParseWebhookEvent(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new StripeWebhookEvent
            {
                EventType            = "customer.subscription.updated",
                StripeSubscriptionId = StripeSubscriptionId,
                Plan                 = PlanTier.Professional,
            });

        await CreateHandler(db).Handle(new HandleStripeWebhookCommand("payload", "sig"), default);

        db.AuditLog.Should().ContainSingle();
        var entry = db.AuditLog[0];
        entry.OldPlan.Should().Be("Starter");
        entry.NewPlan.Should().Be("Professional");
        entry.Source.Should().Contain("subscription.updated");
    }

    [Fact]
    public async Task Handle_SubscriptionUpdated_NoAuditEntry_WhenPlanUnchanged()
    {
        var db = BuildInMemoryDb();
        var sub = Subscription.Create(TenantId, StripeCustomerId);
        sub.Activate(StripeSubscriptionId, PlanTier.Starter, null, null);
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        // Plan stays Starter — only dates updated
        _stripe.ParseWebhookEvent(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new StripeWebhookEvent
            {
                EventType            = "customer.subscription.updated",
                StripeSubscriptionId = StripeSubscriptionId,
                Plan                 = PlanTier.Starter,
                CurrentPeriodEnd     = DateTime.UtcNow.AddDays(30),
            });

        await CreateHandler(db).Handle(new HandleStripeWebhookCommand("payload", "sig"), default);

        db.AuditLog.Should().BeEmpty();
        await _tm.DidNotReceive().UpdateTenantPlanAsync(
            Arg.Any<Guid>(), Arg.Any<PlanTier>(), Arg.Any<CancellationToken>());
    }

    // ─── invoice.payment_failed ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_PaymentFailed_MarksSubscriptionPastDue()
    {
        var db = BuildInMemoryDb();
        var sub = Subscription.Create(TenantId, StripeCustomerId);
        sub.Activate(StripeSubscriptionId, PlanTier.Starter, null, null);
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        _stripe.ParseWebhookEvent(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new StripeWebhookEvent
            {
                EventType            = "invoice.payment_failed",
                StripeSubscriptionId = StripeSubscriptionId,
            });

        await CreateHandler(db).Handle(new HandleStripeWebhookCommand("payload", "sig"), default);

        var updated = await db.Subscriptions.SingleAsync();
        updated.Status.Should().Be(SubscriptionStatus.PastDue);
        updated.Plan.Should().Be(PlanTier.Starter);
        // Payment failure is a status change only — no plan change, no audit entry
        await _tm.DidNotReceive().UpdateTenantPlanAsync(
            Arg.Any<Guid>(), Arg.Any<PlanTier>(), Arg.Any<CancellationToken>());
        db.AuditLog.Should().BeEmpty();
    }

    // ─── Invalid webhook signature ────────────────────────────────────────────

    [Fact]
    public async Task Handle_InvalidSignature_ThrowsWebhookSignatureException()
    {
        var db = BuildInMemoryDb();

        _stripe.ParseWebhookEvent(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Throws(new WebhookSignatureException());

        var act = async () => await CreateHandler(db).Handle(
            new HandleStripeWebhookCommand("bad", "bad-sig"), default);

        await act.Should().ThrowAsync<WebhookSignatureException>();
    }

    // ─── Unknown event type ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UnknownEventType_DoesNothing()
    {
        var db = BuildInMemoryDb();

        _stripe.ParseWebhookEvent(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new StripeWebhookEvent { EventType = "some.other.event" });

        await CreateHandler(db).Handle(new HandleStripeWebhookCommand("payload", "sig"), default);

        var count = await db.Subscriptions.CountAsync();
        count.Should().Be(0);
        await _tm.DidNotReceive().UpdateTenantPlanAsync(
            Arg.Any<Guid>(), Arg.Any<PlanTier>(), Arg.Any<CancellationToken>());
        db.AuditLog.Should().BeEmpty();
    }

    // ─── Missing required fields ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_CheckoutSessionCompleted_MissingTenantId_SkipsWithoutException()
    {
        var db = BuildInMemoryDb();

        _stripe.ParseWebhookEvent(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new StripeWebhookEvent
            {
                EventType            = "checkout.session.completed",
                TenantId             = null,  // missing
                Plan                 = PlanTier.Starter,
                StripeCustomerId     = StripeCustomerId,
                StripeSubscriptionId = StripeSubscriptionId,
            });

        // Should not throw — gracefully skip malformed events
        var act = async () => await CreateHandler(db).Handle(
            new HandleStripeWebhookCommand("payload", "sig"), default);

        await act.Should().NotThrowAsync();
        db.Subscriptions.Should().BeEmpty();
        db.AuditLog.Should().BeEmpty();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static TestBillingDbContext BuildInMemoryDb()
        => new(new DbContextOptionsBuilder<TestBillingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IConfiguration BuildConfig()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stripe:WebhookSecret"] = "whsec_test"
            })
            .Build();
}

// ─── In-memory DbContext for tests ───────────────────────────────────────────
// Avoids Npgsql/Infrastructure dependency. AddPlanChangeAudit writes to an
// in-memory list so tests can assert audit behaviour without a real DB.
internal sealed class TestBillingDbContext : DbContext, IBillingDbContext
{
    public TestBillingDbContext(DbContextOptions<TestBillingDbContext> options) : base(options) { }

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    public List<(Guid TenantId, string OldPlan, string NewPlan, string Source, string? StripeEventId)>
        AuditLog { get; } = [];

    public void AddPlanChangeAudit(
        Guid tenantId, string oldPlan, string newPlan,
        string source, string? stripeEventId = null)
        => AuditLog.Add((tenantId, oldPlan, newPlan, source, stripeEventId));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subscription>().HasKey(s => s.Id);
        modelBuilder.Entity<Subscription>().Property(s => s.Plan).HasConversion<string>();
        modelBuilder.Entity<Subscription>().Property(s => s.Status).HasConversion<string>();
    }
}
