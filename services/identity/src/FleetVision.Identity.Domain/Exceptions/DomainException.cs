namespace FleetVision.Identity.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public sealed class TenantNotFoundException : DomainException
{
    public TenantNotFoundException(Guid id) : base($"Tenant {id} not found.") { }
}

public sealed class UserNotFoundException : DomainException
{
    public UserNotFoundException(string email) : base("User not found.") { }
    public UserNotFoundException(Guid id) : base("User not found.") { }
}

public sealed class DuplicateTenantSlugException : DomainException
{
    public DuplicateTenantSlugException(string slug) : base("A tenant with this identifier already exists.") { }
}

public sealed class DuplicateEmailException : DomainException
{
    public DuplicateEmailException() : base("An account with this email already exists.") { }
}

public sealed class InvalidCredentialsException : DomainException
{
    public InvalidCredentialsException() : base("Invalid email or password.") { }
}

public sealed class AccountInactiveException : DomainException
{
    public AccountInactiveException() : base("This account has been deactivated.") { }
}

public sealed class InvalidRefreshTokenException : DomainException
{
    public InvalidRefreshTokenException() : base("Invalid or expired refresh token.") { }
}
