using FleetVision.FleetAssets.Domain.Enums;
using FleetVision.FleetAssets.Domain.Exceptions;

namespace FleetVision.FleetAssets.Domain.Entities;

public sealed class Driver
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string LicenseNumber { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public DriverStatus Status { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Driver() { }

    public static Driver Create(
        Guid tenantId, string fullName, string licenseNumber,
        string? phone = null, string? email = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(fullName))      throw new ArgumentException("FullName is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(licenseNumber)) throw new ArgumentException("LicenseNumber is required.", nameof(licenseNumber));

        return new Driver
        {
            Id            = Guid.NewGuid(),
            TenantId      = tenantId,
            FullName      = fullName.Trim(),
            LicenseNumber = licenseNumber.Trim(),
            Phone         = phone?.Trim(),
            Email         = email?.Trim().ToLowerInvariant(),
            Status        = DriverStatus.Active,
            IsDeleted     = false,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow
        };
    }

    public void Update(string fullName, string licenseNumber, string? phone, string? email, DriverStatus status)
    {
        if (string.IsNullOrWhiteSpace(fullName))      throw new ArgumentException("FullName is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(licenseNumber)) throw new ArgumentException("LicenseNumber is required.", nameof(licenseNumber));

        FullName      = fullName.Trim();
        LicenseNumber = licenseNumber.Trim();
        Phone         = phone?.Trim();
        Email         = email?.Trim().ToLowerInvariant();
        Status        = status;
        UpdatedAt     = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        if (IsDeleted) throw new DriverAlreadyDeletedException(Id);
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
