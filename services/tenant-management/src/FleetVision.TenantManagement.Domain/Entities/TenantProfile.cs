using FleetVision.TenantManagement.Domain.Enums;
using FleetVision.TenantManagement.Domain.Exceptions;
using FleetVision.TenantManagement.Domain.ValueObjects;

namespace FleetVision.TenantManagement.Domain.Entities;

public sealed class TenantProfile
{
    public Guid Id { get; private set; }

    /// <summary>Shared key with Identity service — the external tenant identifier.</summary>
    public Guid TenantId { get; private set; }

    public string CompanyName { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public PlanTier Plan { get; private set; }
    public int MaxVehicles { get; private set; }
    public int MaxUsers { get; private set; }
    public string BillingEmail { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private TenantProfile() { }

    public static TenantProfile Create(
        Guid tenantId,
        string companyName,
        string slug,
        string billingEmail,
        PlanTier plan = PlanTier.Free)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(companyName))
            throw new ArgumentException("Company name is required.", nameof(companyName));
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Slug is required.", nameof(slug));
        if (string.IsNullOrWhiteSpace(billingEmail))
            throw new ArgumentException("Billing email is required.", nameof(billingEmail));

        var now = DateTime.UtcNow;
        return new TenantProfile
        {
            Id          = Guid.NewGuid(),
            TenantId    = tenantId,
            CompanyName = companyName.Trim(),
            Slug        = slug.Trim().ToLowerInvariant(),
            BillingEmail = billingEmail.Trim().ToLowerInvariant(),
            Plan        = plan,
            MaxVehicles = PlanLimits.MaxVehiclesFor(plan),
            MaxUsers    = PlanLimits.MaxUsersFor(plan),
            IsActive    = true,
            CreatedAt   = now,
            UpdatedAt   = now
        };
    }

    /// <summary>Upgrades are always allowed; downgrades require explicit support action.</summary>
    public void ChangePlan(PlanTier newPlan)
    {
        if (newPlan < Plan)
            throw new PlanDowngradeNotAllowedException(Plan.ToString(), newPlan.ToString());

        Plan        = newPlan;
        MaxVehicles = PlanLimits.MaxVehiclesFor(newPlan);
        MaxUsers    = PlanLimits.MaxUsersFor(newPlan);
        UpdatedAt   = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive  = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive  = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Called exclusively by Billing Service via the internal /internal/tenants/{id}/plan endpoint.
    /// Bypasses the upgrade-only restriction of ChangePlan() to allow Stripe-driven downgrades.
    /// </summary>
    public void SetPlanByBilling(PlanTier newPlan)
    {
        Plan        = newPlan;
        MaxVehicles = PlanLimits.MaxVehiclesFor(newPlan);
        MaxUsers    = PlanLimits.MaxUsersFor(newPlan);
        UpdatedAt   = DateTime.UtcNow;
    }
}
