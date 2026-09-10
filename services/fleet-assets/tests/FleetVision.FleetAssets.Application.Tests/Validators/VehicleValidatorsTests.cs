using FleetVision.FleetAssets.Application.Vehicles.Commands;
using FleetVision.FleetAssets.Domain.Enums;
using FluentValidation.TestHelper;

namespace FleetVision.FleetAssets.Application.Tests.Validators;

// Asevera las reglas REALES de los validators de Vehicle (leidas de los archivos de comando),
// no reglas inventadas. Un caso valido + un caso invalido por cada regla.
public sealed class VehicleValidatorsTests
{
    // ---------------------------------------------------------------------
    // CreateVehicleCommandValidator
    // ---------------------------------------------------------------------
    public sealed class CreateVehicle
    {
        private readonly CreateVehicleCommandValidator _validator = new();

        private static CreateVehicleCommand Valid() =>
            new(Guid.NewGuid(), Guid.NewGuid(), "ABC-123", null, "Toyota", "Hilux", 2022, 0);

        [Fact]
        public void Valid_ShouldNotHaveErrors()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void ValidBoundaries_ShouldNotHaveErrors()
        {
            // Vin de 17 chars exactos, Year = UtcNow.Year + 1, OdometerKm = 0
            var cmd = Valid() with
            {
                Vin        = "1HGCM82633A004352", // 17 chars
                Year       = DateTime.UtcNow.Year + 1,
                OdometerKm = 0
            };

            _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void VinNull_ShouldNotTriggerVinLengthRule()
        {
            var cmd = Valid() with { Vin = null };

            _validator.TestValidate(cmd).ShouldNotHaveValidationErrorFor(x => x.Vin);
        }

        [Fact]
        public void EmptyTenantId_ShouldHaveError()
        {
            var cmd = Valid() with { TenantId = Guid.Empty };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.TenantId);
        }

        [Fact]
        public void EmptyFleetId_ShouldHaveError()
        {
            var cmd = Valid() with { FleetId = Guid.Empty };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.FleetId);
        }

        [Fact]
        public void EmptyPlate_ShouldHaveError()
        {
            var cmd = Valid() with { Plate = "" };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Plate);
        }

        [Fact]
        public void PlateTooLong_ShouldHaveError()
        {
            var cmd = Valid() with { Plate = new string('A', 21) };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Plate);
        }

        [Fact]
        public void VinTooLong_ShouldHaveError()
        {
            var cmd = Valid() with { Vin = new string('1', 18) };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Vin);
        }

        [Fact]
        public void EmptyBrand_ShouldHaveError()
        {
            var cmd = Valid() with { Brand = "" };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Brand);
        }

        [Fact]
        public void BrandTooLong_ShouldHaveError()
        {
            var cmd = Valid() with { Brand = new string('B', 51) };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Brand);
        }

        [Fact]
        public void EmptyModel_ShouldHaveError()
        {
            var cmd = Valid() with { Model = "" };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Model);
        }

        [Fact]
        public void ModelTooLong_ShouldHaveError()
        {
            var cmd = Valid() with { Model = new string('M', 51) };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Model);
        }

        [Fact]
        public void YearBelowMinimum_ShouldHaveError()
        {
            var cmd = Valid() with { Year = 1899 };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Year);
        }

        [Fact]
        public void YearAboveMaximum_ShouldHaveError()
        {
            var cmd = Valid() with { Year = DateTime.UtcNow.Year + 2 };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Year);
        }

        [Fact]
        public void NegativeOdometer_ShouldHaveError()
        {
            var cmd = Valid() with { OdometerKm = -1 };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.OdometerKm);
        }
    }

    // ---------------------------------------------------------------------
    // UpdateVehicleCommandValidator
    // ---------------------------------------------------------------------
    public sealed class UpdateVehicle
    {
        private readonly UpdateVehicleCommandValidator _validator = new();

        private static UpdateVehicleCommand Valid() =>
            new(Guid.NewGuid(), Guid.NewGuid(), "ABC-123", "Toyota", "Hilux", 2022, 100, VehicleStatus.Active);

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
        public void EmptyPlate_ShouldHaveError()
        {
            var cmd = Valid() with { Plate = "" };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Plate);
        }

        [Fact]
        public void PlateTooLong_ShouldHaveError()
        {
            var cmd = Valid() with { Plate = new string('A', 21) };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Plate);
        }

        [Fact]
        public void EmptyBrand_ShouldHaveError()
        {
            var cmd = Valid() with { Brand = "" };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Brand);
        }

        [Fact]
        public void EmptyModel_ShouldHaveError()
        {
            var cmd = Valid() with { Model = "" };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Model);
        }

        [Fact]
        public void YearBelowMinimum_ShouldHaveError()
        {
            var cmd = Valid() with { Year = 1899 };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Year);
        }

        [Fact]
        public void NegativeOdometer_ShouldHaveError()
        {
            var cmd = Valid() with { OdometerKm = -1 };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.OdometerKm);
        }

        [Fact]
        public void BrandTooLong_ShouldHaveError()
        {
            var cmd = Valid() with { Brand = new string('B', 51) };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Brand);
        }

        [Fact]
        public void ModelTooLong_ShouldHaveError()
        {
            var cmd = Valid() with { Model = new string('M', 51) };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Model);
        }

        [Fact]
        public void YearAboveMaximum_ShouldHaveError()
        {
            var cmd = Valid() with { Year = DateTime.UtcNow.Year + 2 };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Year);
        }

        [Fact]
        public void StatusOutOfEnum_ShouldHaveError()
        {
            var cmd = Valid() with { Status = (VehicleStatus)999 };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Status);
        }
    }

    // ---------------------------------------------------------------------
    // UpdateVehiclePositionCommandValidator
    // ---------------------------------------------------------------------
    public sealed class UpdateVehiclePosition
    {
        private readonly UpdateVehiclePositionCommandValidator _validator = new();

        private static UpdateVehiclePositionCommand Valid() =>
            new(Guid.NewGuid(), Guid.NewGuid(), -74.08, 4.60);

        [Fact]
        public void Valid_ShouldNotHaveErrors()
        {
            _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData(-180, -90)]
        [InlineData(180, 90)]
        public void ValidBoundaries_ShouldNotHaveErrors(double longitude, double latitude)
        {
            var cmd = Valid() with { Longitude = longitude, Latitude = latitude };

            _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
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
        public void LongitudeBelowMinimum_ShouldHaveError()
        {
            var cmd = Valid() with { Longitude = -180.01 };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Longitude);
        }

        [Fact]
        public void LongitudeAboveMaximum_ShouldHaveError()
        {
            var cmd = Valid() with { Longitude = 180.01 };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Longitude);
        }

        [Fact]
        public void LatitudeBelowMinimum_ShouldHaveError()
        {
            var cmd = Valid() with { Latitude = -90.01 };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Latitude);
        }

        [Fact]
        public void LatitudeAboveMaximum_ShouldHaveError()
        {
            var cmd = Valid() with { Latitude = 90.01 };

            _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Latitude);
        }
    }
}
