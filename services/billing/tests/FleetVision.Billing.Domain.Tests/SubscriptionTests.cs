using FleetVision.Billing.Domain.Entities;
using FleetVision.Billing.Domain.Enums;
using FluentAssertions;

namespace FleetVision.Billing.Domain.Tests;

public sealed class SubscriptionTests
{
    private static readonly Guid TenantId        = Guid.NewGuid();
    private const string StripeCustomerId        = "cus_test123";
    private const string StripeSubscriptionId    = "sub_test123";

    // ─── Create ───────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidArgs_SetsDefaults()
    {
        var sub = Subscription.Create(TenantId, StripeCustomerId);

        sub.TenantId.Should().Be(TenantId);
        sub.StripeCustomerId.Should().Be(StripeCustomerId);
        sub.Plan.Should().Be(PlanTier.Free);
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.StripeSubscriptionId.Should().BeNull();
        sub.CancelAtPeriodEnd.Should().BeFalse();
    }

    [Fact]
    public void Create_EmptyTenantId_Throws()
    {
        var act = () => Subscription.Create(Guid.Empty, StripeCustomerId);
        act.Should().Throw<ArgumentException>().WithMessage("*TenantId*");
    }

    [Fact]
    public void Create_EmptyCustomerId_Throws()
    {
        var act = () => Subscription.Create(TenantId, string.Empty);
        act.Should().Throw<ArgumentException>().WithMessage("*StripeCustomerId*");
    }

    // ─── Activate ─────────────────────────────────────────────────────────────

    [Fact]
    public void Activate_SetsPlanAndStatus()
    {
        var sub = Subscription.Create(TenantId, StripeCustomerId);
        var periodStart = DateTime.UtcNow;
        var periodEnd   = periodStart.AddDays(30);

        sub.Activate(StripeSubscriptionId, PlanTier.Starter, periodStart, periodEnd);

        sub.StripeSubscriptionId.Should().Be(StripeSubscriptionId);
        sub.Plan.Should().Be(PlanTier.Starter);
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.CurrentPeriodStart.Should().BeCloseTo(periodStart, TimeSpan.FromSeconds(1));
        sub.CurrentPeriodEnd.Should().BeCloseTo(periodEnd, TimeSpan.FromSeconds(1));
        sub.CancelAtPeriodEnd.Should().BeFalse();
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    [Fact]
    public void Update_ChangePlanAndDates()
    {
        var sub = Subscription.Create(TenantId, StripeCustomerId);
        sub.Activate(StripeSubscriptionId, PlanTier.Starter, null, null);

        var newEnd = DateTime.UtcNow.AddDays(30);
        sub.Update(PlanTier.Professional, DateTime.UtcNow, newEnd, false);

        sub.Plan.Should().Be(PlanTier.Professional);
        sub.Status.Should().Be(SubscriptionStatus.Active);
    }

    // ─── Cancel ───────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_DowngradesToFreeAndMarkedCanceled()
    {
        var sub = Subscription.Create(TenantId, StripeCustomerId);
        sub.Activate(StripeSubscriptionId, PlanTier.Professional, null, null);

        sub.Cancel();

        sub.Plan.Should().Be(PlanTier.Free);
        sub.Status.Should().Be(SubscriptionStatus.Canceled);
    }

    // ─── MarkPastDue ──────────────────────────────────────────────────────────

    [Fact]
    public void MarkPastDue_SetsStatusWithoutChangingPlan()
    {
        var sub = Subscription.Create(TenantId, StripeCustomerId);
        sub.Activate(StripeSubscriptionId, PlanTier.Starter, null, null);

        sub.MarkPastDue();

        sub.Status.Should().Be(SubscriptionStatus.PastDue);
        sub.Plan.Should().Be(PlanTier.Starter);
    }

    // ─── SetCancelAtPeriodEnd ─────────────────────────────────────────────────

    [Fact]
    public void SetCancelAtPeriodEnd_UpdatesFlag()
    {
        var sub = Subscription.Create(TenantId, StripeCustomerId);
        sub.Activate(StripeSubscriptionId, PlanTier.Enterprise, null, null);

        sub.SetCancelAtPeriodEnd(true);

        sub.CancelAtPeriodEnd.Should().BeTrue();
        sub.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public void SetCancelAtPeriodEnd_CanBeReverted()
    {
        var sub = Subscription.Create(TenantId, StripeCustomerId);
        sub.Activate(StripeSubscriptionId, PlanTier.Starter, null, null);
        sub.SetCancelAtPeriodEnd(true);

        sub.SetCancelAtPeriodEnd(false);

        sub.CancelAtPeriodEnd.Should().BeFalse();
    }
}
