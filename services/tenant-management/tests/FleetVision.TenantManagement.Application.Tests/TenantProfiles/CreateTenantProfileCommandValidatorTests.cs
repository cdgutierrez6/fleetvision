using FleetVision.TenantManagement.Application.TenantProfiles.Commands.CreateTenantProfile;
using FluentValidation.TestHelper;
using Xunit;

namespace FleetVision.TenantManagement.Application.Tests.TenantProfiles;

public sealed class CreateTenantProfileCommandValidatorTests
{
    private readonly CreateTenantProfileCommandValidator _validator = new();

    private static CreateTenantProfileCommand Valid(
        Guid? tenantId = null,
        string companyName = "Acme Corp",
        string slug = "acme-corp",
        string billingEmail = "billing@acme.com")
        => new(tenantId ?? Guid.NewGuid(), companyName, slug, billingEmail);

    [Fact]
    public void Validate_WithValidCommand_ShouldHaveNoErrors()
    {
        var result = _validator.TestValidate(Valid());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyTenantId_ShouldFail()
    {
        var result = _validator.TestValidate(Valid(tenantId: Guid.Empty));

        result.ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    [Fact]
    public void Validate_EmptyCompanyName_ShouldFail()
    {
        var result = _validator.TestValidate(Valid(companyName: ""));

        result.ShouldHaveValidationErrorFor(x => x.CompanyName);
    }

    [Fact]
    public void Validate_CompanyNameTooLong_ShouldFail()
    {
        var result = _validator.TestValidate(Valid(companyName: new string('a', 201)));

        result.ShouldHaveValidationErrorFor(x => x.CompanyName);
    }

    [Fact]
    public void Validate_CompanyNameAtMaxLength_ShouldPass()
    {
        var result = _validator.TestValidate(Valid(companyName: new string('a', 200)));

        result.ShouldNotHaveValidationErrorFor(x => x.CompanyName);
    }

    [Fact]
    public void Validate_EmptySlug_ShouldFail()
    {
        var result = _validator.TestValidate(Valid(slug: ""));

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Fact]
    public void Validate_SlugTooLong_ShouldFail()
    {
        var result = _validator.TestValidate(Valid(slug: new string('a', 101)));

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Theory]
    [InlineData("Acme-Corp")]   // uppercase not allowed
    [InlineData("acme corp")]   // space not allowed
    [InlineData("acme_corp")]   // underscore not allowed
    [InlineData("acme.corp")]   // dot not allowed
    public void Validate_SlugWithInvalidCharacters_ShouldFail(string slug)
    {
        var result = _validator.TestValidate(Valid(slug: slug));

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    [Theory]
    [InlineData("acme-corp-2")]
    [InlineData("acme")]
    [InlineData("123")]
    public void Validate_SlugWithValidCharacters_ShouldPass(string slug)
    {
        var result = _validator.TestValidate(Valid(slug: slug));

        result.ShouldNotHaveValidationErrorFor(x => x.Slug);
    }

    [Fact]
    public void Validate_EmptyBillingEmail_ShouldFail()
    {
        var result = _validator.TestValidate(Valid(billingEmail: ""));

        result.ShouldHaveValidationErrorFor(x => x.BillingEmail);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("no-at-sign.com")]
    [InlineData("@nodomain.com")]
    public void Validate_InvalidBillingEmail_ShouldFail(string email)
    {
        var result = _validator.TestValidate(Valid(billingEmail: email));

        result.ShouldHaveValidationErrorFor(x => x.BillingEmail);
    }

    [Fact]
    public void Validate_ValidBillingEmail_ShouldPass()
    {
        var result = _validator.TestValidate(Valid(billingEmail: "ops@fleet.io"));

        result.ShouldNotHaveValidationErrorFor(x => x.BillingEmail);
    }
}
