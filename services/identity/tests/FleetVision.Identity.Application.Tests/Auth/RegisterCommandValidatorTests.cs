using FleetVision.Identity.Application.Auth.Commands.Register;
using FluentValidation.TestHelper;

namespace FleetVision.Identity.Application.Tests.Auth;

public sealed class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    private static RegisterCommand Valid(
        string companyName = "Acme Corp",
        string email = "admin@acme.com",
        string password = "Secure123!",
        string firstName = "Juan",
        string lastName = "Gomez")
        => new(companyName, email, password, firstName, lastName);

    [Fact]
    public void Validate_WithValidCommand_ShouldHaveNoErrors()
    {
        var result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyCompanyName_ShouldHaveError(string companyName)
    {
        var result = _validator.TestValidate(Valid(companyName: companyName));
        result.ShouldHaveValidationErrorFor(x => x.CompanyName);
    }

    [Fact]
    public void Validate_WithCompanyNameTooLong_ShouldHaveError()
    {
        var result = _validator.TestValidate(Valid(companyName: new string('a', 256)));
        result.ShouldHaveValidationErrorFor(x => x.CompanyName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyEmail_ShouldHaveError(string email)
    {
        var result = _validator.TestValidate(Valid(email: email));
        result.ShouldHaveValidationErrorFor(x => x.AdminEmail);
    }

    [Fact]
    public void Validate_WithInvalidEmailFormat_ShouldHaveError()
    {
        var result = _validator.TestValidate(Valid(email: "not-an-email"));
        result.ShouldHaveValidationErrorFor(x => x.AdminEmail);
    }

    [Fact]
    public void Validate_WithEmailTooLong_ShouldHaveError()
    {
        // >255 chars while still a valid-looking address (256-char local part + "@a.co").
        var longEmail = new string('a', 256) + "@a.co";
        var result = _validator.TestValidate(Valid(email: longEmail));
        result.ShouldHaveValidationErrorFor(x => x.AdminEmail);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyPassword_ShouldHaveError(string password)
    {
        var result = _validator.TestValidate(Valid(password: password));
        result.ShouldHaveValidationErrorFor(x => x.AdminPassword);
    }

    [Fact]
    public void Validate_WithPasswordTooShort_ShouldHaveError()
    {
        var result = _validator.TestValidate(Valid(password: "Ab1"));
        result.ShouldHaveValidationErrorFor(x => x.AdminPassword);
    }

    [Fact]
    public void Validate_WithPasswordMissingUppercase_ShouldHaveError()
    {
        var result = _validator.TestValidate(Valid(password: "secure123"));
        result.ShouldHaveValidationErrorFor(x => x.AdminPassword);
    }

    [Fact]
    public void Validate_WithPasswordMissingDigit_ShouldHaveError()
    {
        var result = _validator.TestValidate(Valid(password: "SecurePass"));
        result.ShouldHaveValidationErrorFor(x => x.AdminPassword);
    }

    [Fact]
    public void Validate_WithPasswordTooLong_ShouldHaveError()
    {
        // >128 chars, includes an uppercase + digit so only MaximumLength trips.
        var longPassword = "A1" + new string('b', 128);
        var result = _validator.TestValidate(Valid(password: longPassword));
        result.ShouldHaveValidationErrorFor(x => x.AdminPassword);
    }

    [Fact]
    public void Validate_WithPasswordAtLowerBoundary_ShouldNotHavePasswordError()
    {
        // Exactly 8 chars, one uppercase, one digit — the minimal valid password.
        var result = _validator.TestValidate(Valid(password: "Abcdefg1"));
        result.ShouldNotHaveValidationErrorFor(x => x.AdminPassword);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyFirstName_ShouldHaveError(string firstName)
    {
        var result = _validator.TestValidate(Valid(firstName: firstName));
        result.ShouldHaveValidationErrorFor(x => x.AdminFirstName);
    }

    [Fact]
    public void Validate_WithFirstNameTooLong_ShouldHaveError()
    {
        var result = _validator.TestValidate(Valid(firstName: new string('a', 101)));
        result.ShouldHaveValidationErrorFor(x => x.AdminFirstName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyLastName_ShouldHaveError(string lastName)
    {
        var result = _validator.TestValidate(Valid(lastName: lastName));
        result.ShouldHaveValidationErrorFor(x => x.AdminLastName);
    }

    [Fact]
    public void Validate_WithLastNameTooLong_ShouldHaveError()
    {
        var result = _validator.TestValidate(Valid(lastName: new string('a', 101)));
        result.ShouldHaveValidationErrorFor(x => x.AdminLastName);
    }
}
