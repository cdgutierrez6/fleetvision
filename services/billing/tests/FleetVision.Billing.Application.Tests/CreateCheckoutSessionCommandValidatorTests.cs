using FleetVision.Billing.Application.Subscriptions.Commands.CreateCheckoutSession;
using FleetVision.Billing.Domain.Enums;
using FluentValidation.TestHelper;

namespace FleetVision.Billing.Application.Tests;

public sealed class CreateCheckoutSessionCommandValidatorTests
{
    private readonly CreateCheckoutSessionCommandValidator _validator = new();

    private static CreateCheckoutSessionCommand Valid(
        Guid? tenantId = null, string? email = null, PlanTier plan = PlanTier.Starter)
        => new(
            tenantId ?? Guid.NewGuid(),
            email ?? "billing@tenant.test",
            plan);

    // ─── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public void Validate_ValidCommand_PassesWithoutErrors()
    {
        var result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ─── TenantId ────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_EmptyTenantId_HasError()
    {
        var result = _validator.TestValidate(Valid(tenantId: Guid.Empty));
        result.ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    // ─── BillingEmail ────────────────────────────────────────────────────────

    [Fact]
    public void Validate_EmptyEmail_HasError()
    {
        var result = _validator.TestValidate(Valid(email: ""));
        result.ShouldHaveValidationErrorFor(x => x.BillingEmail);
    }

    [Fact]
    public void Validate_MalformedEmail_HasError()
    {
        var result = _validator.TestValidate(Valid(email: "not-an-email"));
        result.ShouldHaveValidationErrorFor(x => x.BillingEmail);
    }

    [Fact]
    public void Validate_EmailExceedingMaxLength_HasError()
    {
        var tooLong = new string('a', 250) + "@tenant.test"; // > 256 chars
        var result = _validator.TestValidate(Valid(email: tooLong));
        result.ShouldHaveValidationErrorFor(x => x.BillingEmail);
    }

    // ─── Plan ────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_FreePlan_HasError()
    {
        var result = _validator.TestValidate(Valid(plan: PlanTier.Free));
        result.ShouldHaveValidationErrorFor(x => x.Plan);
    }

    [Fact]
    public void Validate_UndefinedPlanTier_HasError()
    {
        var result = _validator.TestValidate(Valid(plan: (PlanTier)99));
        result.ShouldHaveValidationErrorFor(x => x.Plan);
    }

    [Theory]
    [InlineData(PlanTier.Starter)]
    [InlineData(PlanTier.Professional)]
    [InlineData(PlanTier.Enterprise)]
    public void Validate_PaidPlans_HaveNoPlanError(PlanTier plan)
    {
        var result = _validator.TestValidate(Valid(plan: plan));
        result.ShouldNotHaveValidationErrorFor(x => x.Plan);
    }
}
