using FleetVision.Identity.Domain.Entities;
using FluentAssertions;

namespace FleetVision.Identity.Domain.Tests.Entities;

public sealed class RefreshTokenTests
{
    [Fact]
    public void Create_ShouldCreateActiveToken()
    {
        var userId = Guid.NewGuid();
        var token = RefreshToken.Create(userId, "hash123", ttlDays: 30);

        token.Id.Should().NotBeEmpty();
        token.UserId.Should().Be(userId);
        token.TokenHash.Should().Be("hash123");
        token.IsRevoked.Should().BeFalse();
        token.IsActive.Should().BeTrue();
        token.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldSetCorrectExpiry()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", ttlDays: 30);
        token.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Revoke_ShouldSetIsRevokedToTrue()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", ttlDays: 30);
        token.Revoke();

        token.IsRevoked.Should().BeTrue();
        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Revoke_WithReplacementHash_ShouldStoreIt()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "old_hash", ttlDays: 30);
        token.Revoke(replacedByHash: "new_hash");

        token.ReplacedByTokenHash.Should().Be("new_hash");
    }

    [Fact]
    public void IsExpired_WhenTokenTtlIsZero_ShouldBeExpired()
    {
        // Token que expira en 0 días = ya expirado
        var token = RefreshToken.Create(Guid.NewGuid(), "hash", ttlDays: 0);
        token.IsExpired.Should().BeTrue();
    }
}
