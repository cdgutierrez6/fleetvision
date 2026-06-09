using FluentValidation;

namespace FleetVision.TenantManagement.Application.TenantProfiles.Commands.CreateTenantProfile;

public sealed class CreateTenantProfileCommandValidator : AbstractValidator<CreateTenantProfileCommand>
{
    public CreateTenantProfileCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required.");

        RuleFor(x => x.CompanyName)
            .NotEmpty().WithMessage("Company name is required.")
            .MaximumLength(200).WithMessage("Company name must not exceed 200 characters.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required.")
            .MaximumLength(100).WithMessage("Slug must not exceed 100 characters.")
            .Matches(@"^[a-z0-9-]+$").WithMessage("Slug can only contain lowercase letters, numbers, and hyphens.");

        RuleFor(x => x.BillingEmail)
            .NotEmpty().WithMessage("Billing email is required.")
            .EmailAddress().WithMessage("Billing email must be a valid email address.");
    }
}
