namespace FleetVision.FleetAssets.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public sealed class FleetNotFoundException : DomainException
{
    public FleetNotFoundException(Guid id) : base($"Fleet '{id}' not found.") { }
}

public sealed class VehicleNotFoundException : DomainException
{
    public VehicleNotFoundException(Guid id) : base($"Vehicle '{id}' not found.") { }
}

public sealed class DriverNotFoundException : DomainException
{
    public DriverNotFoundException(Guid id) : base($"Driver '{id}' not found.") { }
}

public sealed class AssignmentNotFoundException : DomainException
{
    public AssignmentNotFoundException(Guid vehicleId)
        : base($"No active assignment found for vehicle '{vehicleId}'.") { }
}

public sealed class ActiveAssignmentExistsException : DomainException
{
    public ActiveAssignmentExistsException(Guid vehicleId)
        : base($"Vehicle '{vehicleId}' already has an active assignment.") { }
}

public sealed class VehiclePlanLimitExceededException : DomainException
{
    public VehiclePlanLimitExceededException(int max, string plan)
        : base($"Vehicle limit reached for plan {plan} (max: {max}).") { }
}

public sealed class VehicleAlreadyDeletedException : DomainException
{
    public VehicleAlreadyDeletedException(Guid id) : base($"Vehicle '{id}' not found.") { }
}

public sealed class DriverAlreadyDeletedException : DomainException
{
    public DriverAlreadyDeletedException(Guid id) : base($"Driver '{id}' not found.") { }
}

public sealed class DriverInactiveException : DomainException
{
    public DriverInactiveException(Guid id) : base($"Driver '{id}' is inactive and cannot be assigned.") { }
}
