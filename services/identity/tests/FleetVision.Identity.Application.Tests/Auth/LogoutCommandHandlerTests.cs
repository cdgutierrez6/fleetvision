using FleetVision.Identity.Application.Auth.Commands.Logout;
using FleetVision.Identity.Application.Common.Interfaces;
using FleetVision.Identity.Domain.Entities;
using FleetVision.Identity.Domain.Enums;
using FleetVision.Identity.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FleetVision.Identity.Application.Tests.Auth;

public sealed class LogoutCommandHandlerTests : IDisposable
{
    private readonly IdentityDbContext _db;
    private readonly Mock<ITokenService> _tokenMock;
    private readonly LogoutCommandHandler _handler;
    private readonly User _testUser;

    public LogoutCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new IdentityDbContext(options);
        _tokenMock = new Mock<ITokenService>();
        var loggerMock = new Mock<ILogger<LogoutCommandHandler>>();

        _testUser = User.Create(Guid.NewGuid(), "user@test.com", "argon2_hash", "Juan", "Gomez", UserRole.Admin);
        _db.Users.Add(_testUser);
        _db.SaveChanges();

        // The handler hashes the raw incoming token; it must resolve to the stored token's hash.
        _tokenMock.Setup(t => t.HashToken("raw")).Returns("stored_hash");

        _handler = new LogoutCommandHandler(_db, _tokenMock.Object, loggerMock.Object);
    }

    private RefreshToken SeedToken(string tokenHash = "stored_hash", int ttlDays = 30, bool revoked = false)
    {
        var token = RefreshToken.Create(_testUser.Id, tokenHash, ttlDays);
        if (revoked) token.Revoke();
        _db.RefreshTokens.Add(token);
        _db.SaveChanges();
        return token;
    }

    [Fact]
    public async Task Handle_WithActiveStoredToken_ShouldRevokeItEffectively()
    {
        SeedToken();

        await _handler.Handle(new LogoutCommand("raw"), default);

        // SECURITY INVARIANT: the persisted record must be revoked (token no longer usable).
        var persisted = await _db.RefreshTokens.FirstAsync();
        persisted.IsRevoked.Should().BeTrue();
        persisted.ReplacedByTokenHash.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithActiveToken_ShouldNotIssueAnyNewToken()
    {
        SeedToken();

        await _handler.Handle(new LogoutCommand("raw"), default);

        // Logout never mints credentials.
        var count = await _db.RefreshTokens.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithAlreadyRevokedToken_ShouldBeIdempotentAndNotThrow()
    {
        SeedToken(revoked: true);

        var act = async () => await _handler.Handle(new LogoutCommand("raw"), default);

        await act.Should().NotThrowAsync();
        var persisted = await _db.RefreshTokens.FirstAsync();
        persisted.IsRevoked.Should().BeTrue();
        // Idempotent path returns early: ReplacedByTokenHash is not overwritten.
        persisted.ReplacedByTokenHash.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithNonExistentToken_ShouldNotThrowAndNotPersistChanges()
    {
        // No token seeded; the computed hash matches nothing in the DB.
        var act = async () => await _handler.Handle(new LogoutCommand("raw"), default);

        await act.Should().NotThrowAsync();
        var count = await _db.RefreshTokens.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithExpiredButNotRevokedToken_ShouldStillRevokeIt()
    {
        // ttlDays:-1 → expired. Logout only inspects IsRevoked, never IsActive/IsExpired.
        SeedToken(ttlDays: -1);

        await _handler.Handle(new LogoutCommand("raw"), default);

        var persisted = await _db.RefreshTokens.FirstAsync();
        persisted.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldResolveTokenByHash_NotByRawValue()
    {
        SeedToken();

        await _handler.Handle(new LogoutCommand("raw"), default);

        // The raw value is hashed exactly once to find the stored token.
        _tokenMock.Verify(t => t.HashToken("raw"), Times.Once);
    }

    public void Dispose() => _db.Dispose();
}
