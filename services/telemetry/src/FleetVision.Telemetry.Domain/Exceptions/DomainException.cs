namespace FleetVision.Telemetry.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public sealed class InvalidTelemetryException : DomainException
{
    public InvalidTelemetryException(string reason)
        : base($"Invalid telemetry data: {reason}") { }
}
