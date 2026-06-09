using FleetVision.Geofencing.Domain.Entities;

namespace FleetVision.Geofencing.Application.Common;

public interface IViolationPublisher
{
    void Enqueue(GeofenceViolation violation, string geofenceName);
}
