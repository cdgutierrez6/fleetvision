using FleetVision.FleetAssets.Application.VehicleAssignments.Commands;
using FluentValidation.TestHelper;

namespace FleetVision.FleetAssets.Application.Tests.Validators;

// Asevera las reglas REALES de CreateVehicleAssignmentCommandValidator.
public sealed class VehicleAssignmentValidatorTests
{
    private readonly CreateVehicleAssignmentCommandValidator _validator = new();

    private static CreateVehicleAssignmentCommand Valid() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void Valid_ShouldNotHaveErrors()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyTenantId_ShouldHaveError()
    {
        var cmd = Valid() with { TenantId = Guid.Empty };

        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    [Fact]
    public void EmptyVehicleId_ShouldHaveError()
    {
        var cmd = Valid() with { VehicleId = Guid.Empty };

        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.VehicleId);
    }

    [Fact]
    public void EmptyDriverId_ShouldHaveError()
    {
        var cmd = Valid() with { DriverId = Guid.Empty };

        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.DriverId);
    }
}
