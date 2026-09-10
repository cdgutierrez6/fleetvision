using FleetVision.FleetAssets.Application.Fleets.Commands;
using FluentValidation.TestHelper;

namespace FleetVision.FleetAssets.Application.Tests.Validators;

// Asevera las reglas REALES de los validators de Fleet.
public sealed class FleetValidatorsTests
{
    // ---------------------------------------------------------------------
    // CreateFleetCommandValidator
    // ---------------------------------------------------------------------
    public sealed class CreateFleet
    {
        private readonly CreateFleetCommandValidator _validator = new();

        private static CreateFleetCommand Valid() =>
            new(Guid.NewGuid(), "Flota Norte", "Descripcion breve");

        [Fact]
        public void Valid_ShouldNotHaveErrors()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void DescriptionNull_ShouldNotTriggerLengthRule()
        {
            var cmd = Valid() with { Description = null };

            _validator.TestValidate(cmd).ShouldNotHaveValidationErrorFor(x => x.Description);
        }

        [Fact]
        public void EmptyTenantId_ShouldHaveError()
        {
            var cmd = Valid() with { TenantId = Guid.Empty };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.TenantId);
        }

        [Fact]
        public void EmptyName_ShouldHaveError()
        {
            var cmd = Valid() with { Name = "" };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void NameTooLong_ShouldHaveError()
        {
            var cmd = Valid() with { Name = new string('N', 101) };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void DescriptionTooLong_ShouldHaveError()
        {
            var cmd = Valid() with { Description = new string('D', 501) };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Description);
        }
    }

    // ---------------------------------------------------------------------
    // UpdateFleetCommandValidator
    // ---------------------------------------------------------------------
    public sealed class UpdateFleet
    {
        private readonly UpdateFleetCommandValidator _validator = new();

        private static UpdateFleetCommand Valid() =>
            new(Guid.NewGuid(), Guid.NewGuid(), "Flota Sur", null);

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
        public void EmptyName_ShouldHaveError()
        {
            var cmd = Valid() with { Name = "" };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void NameTooLong_ShouldHaveError()
        {
            var cmd = Valid() with { Name = new string('N', 101) };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void DescriptionTooLong_ShouldHaveError()
        {
            var cmd = Valid() with { Description = new string('D', 501) };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Description);
        }
    }
}
