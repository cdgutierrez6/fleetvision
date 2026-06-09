namespace FleetVision.Geofencing.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public sealed class GeofenceNotFoundException : DomainException
{
    public GeofenceNotFoundException(Guid id) : base($"Geofence '{id}' not found.") { }
}

public sealed class ViolationNotFoundException : DomainException
{
    public ViolationNotFoundException(Guid id) : base($"Violation '{id}' not found.") { }
}

public sealed class GeofencePlanLimitExceededException : DomainException
{
    public GeofencePlanLimitExceededException(int max, string plan)
        : base($"Geofence limit reached for plan {plan} (max: {max}).") { }
}

public sealed class GeofenceNameAlreadyExistsException : DomainException
{
    public GeofenceNameAlreadyExistsException(string name)
        : base($"A geofence with name '{name}' already exists in this tenant.") { }
}

public sealed class InvalidPolygonException : DomainException
{
    public InvalidPolygonException(string reason)
        : base($"Invalid polygon: {reason}.") { }
}
