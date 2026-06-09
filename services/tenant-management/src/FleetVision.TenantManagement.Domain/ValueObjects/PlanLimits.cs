using FleetVision.TenantManagement.Domain.Enums;

namespace FleetVision.TenantManagement.Domain.ValueObjects;

/// <summary>
/// Static plan limits — version-controlled so changes are tracked in git.
/// No DB table needed; update and redeploy to change limits.
/// </summary>
public static class PlanLimits
{
    public static int MaxVehiclesFor(PlanTier tier) => tier switch
    {
        PlanTier.Free         => 3,
        PlanTier.Starter      => 25,
        PlanTier.Professional => 100,
        PlanTier.Enterprise   => 1000,
        _                     => throw new ArgumentOutOfRangeException(nameof(tier), tier, null)
    };

    public static int MaxUsersFor(PlanTier tier) => tier switch
    {
        PlanTier.Free         => 5,
        PlanTier.Starter      => 25,
        PlanTier.Professional => 100,
        PlanTier.Enterprise   => int.MaxValue,
        _                     => throw new ArgumentOutOfRangeException(nameof(tier), tier, null)
    };
}
