namespace FleetVision.Identity.Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public DateTime ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    public User User { get; private set; } = default!;

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string tokenHash, int ttlDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(ttlDays),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => !IsRevoked && !IsExpired;

    public void Revoke(string? replacedByHash = null)
    {
        IsRevoked = true;
        ReplacedByTokenHash = replacedByHash;
    }
}
