using FleetVision.Billing.Domain.Enums;
using FluentValidation;

namespace FleetVision.Billing.Application.Subscriptions.Commands.CreateCheckoutSession;

public sealed class CreateCheckoutSessionCommandValidator
    : AbstractValidator<CreateCheckoutSessionCommand>
{
    public CreateCheckoutSessionCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.BillingEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Plan)
            .Must(p => p != PlanTier.Free)
            .WithMessage("Cannot create a checkout session for the Free plan.");
        RuleFor(x => x.Plan)
            .IsInEnum()
            .WithMessage("Invalid plan tier.");
    }
}
