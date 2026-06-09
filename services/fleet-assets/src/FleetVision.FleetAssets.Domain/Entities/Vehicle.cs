using FleetVision.FleetAssets.Domain.Enums;
using FleetVision.FleetAssets.Domain.Exceptions;
using NetTopologySuite.Geometries;

namespace FleetVision.FleetAssets.Domain.Entities;

public sealed class Vehicle
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid FleetId { get; private set; }
    public string Plate { get; private set; } = string.Empty;
    public string? Vin { get; private set; }
    public string Brand { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public int Year { get; private set; }
    public int OdometerKm { get; private set; }
    public VehicleStatus Status { get; private set; }
    public Point? LastKnownPosition { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Vehicle() { }

    public static Vehicle Create(
        Guid tenantId, Guid fleetId, string plate, string? vin,
        string brand, string model, int year, int odometerKm = 0)
    {
        if (tenantId == Guid.Empty)  throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (fleetId == Guid.Empty)   throw new ArgumentException("FleetId cannot be empty.", nameof(fleetId));
        if (string.IsNullOrWhiteSpace(plate)) throw new ArgumentException("Plate is required.", nameof(plate));
        if (string.IsNullOrWhiteSpace(brand)) throw new ArgumentException("Brand is required.", nameof(brand));
        if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("Model is required.", nameof(model));
        if (year < 1900)        throw new ArgumentException("Year must be >= 1900.", nameof(year));
        if (odometerKm < 0)     throw new ArgumentException("Odometer cannot be negative.", nameof(odometerKm));

        return new Vehicle
        {
            Id             = Guid.NewGuid(),
            TenantId       = tenantId,
            FleetId        = fleetId,
            Plate          = plate.Trim().ToUpperInvariant(),
            Vin            = string.IsNullOrWhiteSpace(vin) ? null : vin.Trim().ToUpperInvariant(),
            Brand          = brand.Trim(),
            Model          = model.Trim(),
            Year           = year,
            OdometerKm     = odometerKm,
            Status         = VehicleStatus.Active,
            IsDeleted      = false,
            CreatedAt      = DateTime.UtcNow,
            UpdatedAt      = DateTime.UtcNow
        };
    }

    public void Update(string plate, string brand, string model, int year, int odometerKm, VehicleStatus status)
    {
        if (string.IsNullOrWhiteSpace(plate)) throw new ArgumentException("Plate is required.", nameof(plate));
        if (string.IsNullOrWhiteSpace(brand)) throw new ArgumentException("Brand is required.", nameof(brand));
        if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("Model is required.", nameof(model));
        if (year < 1900)    throw new ArgumentException("Year must be >= 1900.", nameof(year));
        if (odometerKm < 0) throw new ArgumentException("Odometer cannot be negative.", nameof(odometerKm));

        Plate      = plate.Trim().ToUpperInvariant();
        Brand      = brand.Trim();
        Model      = model.Trim();
        Year       = year;
        OdometerKm = odometerKm;
        Status     = status;
        UpdatedAt  = DateTime.UtcNow;
    }

    public void UpdatePosition(double longitude, double latitude)
    {
        if (longitude < -180 || longitude > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude), "Must be between -180 and 180.");
        if (latitude < -90 || latitude > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Must be between -90 and 90.");

        // NTS Point: X = longitude, Y = latitude
        LastKnownPosition = new Point(longitude, latitude) { SRID = 4326 };
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        if (IsDeleted) throw new VehicleAlreadyDeletedException(Id);
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
