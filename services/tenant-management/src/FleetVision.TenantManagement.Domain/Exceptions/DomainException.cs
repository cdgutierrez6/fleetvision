namespace FleetVision.TenantManagement.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public sealed class TenantProfileNotFoundException : DomainException
{
    public TenantProfileNotFoundException(Guid tenantId)
        : base($"Tenant profile not found for tenant '{tenantId}'.") { }
}

public sealed class TenantProfileAlreadyExistsException : DomainException
{
    public TenantProfileAlreadyExistsException(Guid tenantId)
        : base($"A tenant profile already exists for tenant '{tenantId}'.") { }
}

public sealed class PlanDowngradeNotAllowedException : DomainException
{
    public PlanDowngradeNotAllowedException(string current, string requested)
        : base($"Cannot downgrade plan from '{current}' to '{requested}'. Contact support.") { }
}
