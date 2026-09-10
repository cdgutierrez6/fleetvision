using FleetVision.FleetAssets.Application.Drivers.Commands;
using FleetVision.FleetAssets.Domain.Enums;
using FluentValidation.TestHelper;

namespace FleetVision.FleetAssets.Application.Tests.Validators;

// Asevera las reglas REALES de los validators de Driver.
public sealed class DriverValidatorsTests
{
    // ---------------------------------------------------------------------
    // CreateDriverCommandValidator
    // ---------------------------------------------------------------------
    public sealed class CreateDriver
    {
        private readonly CreateDriverCommandValidator _validator = new();

        private static CreateDriverCommand Valid() =>
            new(Guid.NewGuid(), "Juan Perez", "LIC-001", "+573001112233", "juan@test.com");

        [Fact]
        public void Valid_ShouldNotHaveErrors()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void OptionalNulls_ShouldNotTriggerPhoneOrEmailRules()
        {
            var cmd = Valid() with { Phone = null, Email = null };

            var result = _validator.TestValidate(cmd);
            result.ShouldNotHaveValidationErrorFor(x => x.Phone);
            result.ShouldNotHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        public void EmptyTenantId_ShouldHaveError()
        {
            var cmd = Valid() with { TenantId = Guid.Empty };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.TenantId);
        }

        [Fact]
        public void EmptyFullName_ShouldHaveError()
        {
            var cmd = Valid() with { FullName = "" };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.FullName);
        }

        [Fact]
        public void FullNameTooLong_ShouldHaveError()
        {
            var cmd = Valid() with { FullName = new string('N', 101) };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.FullName);
        }

        [Fact]
        public void EmptyLicenseNumber_ShouldHaveError()
        {
            var cmd = Valid() with { LicenseNumber = "" };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.LicenseNumber);
        }

        [Fact]
        public void LicenseNumberTooLong_ShouldHaveError()
        {
            var cmd = Valid() with { LicenseNumber = new string('L', 31) };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.LicenseNumber);
        }

        [Fact]
        public void PhoneTooLong_ShouldHaveError()
        {
            var cmd = Valid() with { Phone = new string('9', 21) };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Phone);
        }

        [Fact]
        public void EmailBadFormat_ShouldHaveError()
        {
            var cmd = Valid() with { Email = "not-an-email" };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        public void EmailTooLong_ShouldHaveError()
        {
            // Formato valido pero > 150 chars => dispara MaximumLength
            var cmd = Valid() with { Email = new string('a', 145) + "@test.com" }; // 154 chars

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Email);
        }
    }

    // ---------------------------------------------------------------------
    // UpdateDriverCommandValidator
    // ---------------------------------------------------------------------
    public sealed class UpdateDriver
    {
        private readonly UpdateDriverCommandValidator _validator = new();

        private static UpdateDriverCommand Valid() =>
            new(Guid.NewGuid(), Guid.NewGuid(), "Juan Perez", "LIC-001", null, null, DriverStatus.Active);

        [Fact]
        public void Valid_ShouldNotHaveErrors()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void EmptyId_ShouldHaveError()
        {
            var cmd = Valid() with { Id = Guid.Empty };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Fact]
        public void EmptyTenantId_ShouldHaveError()
        {
            var cmd = Valid() with { TenantId = Guid.Empty };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.TenantId);
        }

        [Fact]
        public void EmptyFullName_ShouldHaveError()
        {
            var cmd = Valid() with { FullName = "" };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.FullName);
        }

        [Fact]
        public void EmptyLicenseNumber_ShouldHaveError()
        {
            var cmd = Valid() with { LicenseNumber = "" };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.LicenseNumber);
        }

        [Fact]
        public void PhoneTooLong_ShouldHaveError()
        {
            var cmd = Valid() with { Phone = new string('9', 21) };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Phone);
        }

        [Fact]
        public void EmailBadFormat_ShouldHaveError()
        {
            var cmd = Valid() with { Email = "not-an-email" };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        public void FullNameTooLong_ShouldHaveError()
        {
            var cmd = Valid() with { FullName = new string('N', 101) };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.FullName);
        }

        [Fact]
        public void LicenseNumberTooLong_ShouldHaveError()
        {
            var cmd = Valid() with { LicenseNumber = new string('L', 31) };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.LicenseNumber);
        }

        [Fact]
        public void EmailTooLong_ShouldHaveError()
        {
            // Formato valido pero > 150 chars => dispara MaximumLength (no el de formato)
            var cmd = Valid() with { Email = new string('a', 145) + "@test.com" }; // 154 chars

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        public void StatusOutOfEnum_ShouldHaveError()
        {
            var cmd = Valid() with { Status = (DriverStatus)999 };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Status);
        }
    }
}
