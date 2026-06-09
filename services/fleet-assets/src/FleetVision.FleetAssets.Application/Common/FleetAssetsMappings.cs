using FleetVision.FleetAssets.Application.DTOs;
using FleetVision.FleetAssets.Domain.Entities;

namespace FleetVision.FleetAssets.Application.Common;

internal static class FleetAssetsMappings
{
    internal static FleetDto ToDto(Fleet f) =>
        new(f.Id, f.TenantId, f.Name, f.Description, f.CreatedAt, f.UpdatedAt);

    internal static VehicleDto ToDto(Vehicle v) =>
        new(v.Id, v.TenantId, v.FleetId, v.Plate, v.Vin, v.Brand, v.Model,
            v.Year, v.OdometerKm, v.Status.ToString(),
            v.LastKnownPosition?.Y,   // Y = latitude
            v.LastKnownPosition?.X,   // X = longitude
            v.IsDeleted, v.CreatedAt, v.UpdatedAt);

    internal static DriverDto ToDto(Driver d) =>
        new(d.Id, d.TenantId, d.FullName, d.LicenseNumber, d.Phone, d.Email,
            d.Status.ToString(), d.IsDeleted, d.CreatedAt, d.UpdatedAt);

    internal static AssignmentDto ToDto(VehicleAssignment a) =>
        new(a.Id, a.TenantId, a.VehicleId, a.DriverId, a.StartedAt, a.EndedAt);
}
