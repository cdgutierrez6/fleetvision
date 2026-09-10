using FleetVision.Identity.Application.Auth.Commands.Refresh;
using FleetVision.Identity.Application.Common.Interfaces;
using FleetVision.Identity.Domain.Entities;
using FleetVision.Identity.Domain.Enums;
using FleetVision.Identity.Domain.Exceptions;
using FleetVision.Identity.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FleetVision.Identity.Application.Tests.Auth;

public sealed class RefreshCommandHandlerTests : IDisposable
{
    private readonly IdentityDbContext _db;
    private readonly Mock<ITokenService> _tokenMock;
    private readonly RefreshCommandHandler _handler;
    private readonly User _testUser;

    public RefreshCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new IdentityDbContext(options);
        _tokenMock = new Mock<ITokenService>();
        var loggerMock = new Mock<ILogger<RefreshCommandHandler>>();

        _testUser = User.Create(tenantId: Guid.NewGuid(), "user@test.com", "argon2_hash", "Juan", "Gomez", UserRole.Admin);
        _db.Users.Add(_testUser);
        _db.SaveChanges();

        _tokenMock.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("access_token");
        _tokenMock.Setup(t => t.GenerateRefreshToken()).Returns("new_refresh_raw");
        _tokenMock.Setup(t => t.HashToken("new_refresh_raw")).Returns("new_refresh_hash");
        // Incoming raw token resolves to the seeded stored hash.
        _tokenMock.Setup(t => t.HashToken("raw_incoming")).Returns("stored_hash");

        _handler = new RefreshCommandHandler(_db, _tokenMock.Object, loggerMock.Object);
    }

    private RefreshToken SeedToken(string tokenHash = "stored_hash", int ttlDays = 30, bool revoked = false)
    {
        var token = RefreshToken.Create(_testUser.Id, tokenHash, ttlDays);
        if (revoked) token.Revoke();
        _db.RefreshTokens.Add(token);
        _db.SaveChanges();
        return token;
    }

    // The handler MUST reload the token graph itself via .Include(rt => rt.User). We detach every
    // tracked entity right before invoking Handle so EF InMemory's relationship-fixup cannot silently
    // populate storedToken.User: without the handler's own Include, storedToken.User would be null and
    // the User-dereferencing tests would fail — exactly the regression (NullReferenceException on a real
    // relational provider) we want these tests to catch.
    private void DetachAllSoTheHandlerMustReloadViaInclude() => _db.ChangeTracker.Clear();

    [Fact]
    public async Task Handle_WithActiveTokenAndActiveUser_ShouldReturnRawRotatedTokens()
    {
        SeedToken();
        DetachAllSoTheHandlerMustReloadViaInclude();

        var result = await _handler.Handle(new RefreshCommand("raw_incoming"), default);

        result.AccessToken.Should().Be("access_token");
        // SECURITY: the CRUDE new value is returned to the client, never the hash.
        result.RefreshToken.Should().Be("new_refresh_raw");
        result.ExpiresIn.Should().Be(900);
    }

    [Fact]
    public async Task Handle_ShouldLoadUserGraphViaInclude_AndSignForThatUser()
    {
        SeedToken();
        DetachAllSoTheHandlerMustReloadViaInclude();

        await _handler.Handle(new RefreshCommand("raw_incoming"), default);

        // Observable proof that the handler loaded the User via its own Include (nothing is tracked, so
        // the graph could only come from the handler's query): the access token is minted for THAT user.
        // If the handler's Include were removed, storedToken.User would be null and this call would throw.
        _tokenMock.Verify(t => t.GenerateAccessToken(It.Is<User>(u => u.Id == _testUser.Id)), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPersistRotation_OldRevokedChainedToNew()
    {
        var old = SeedToken();
        DetachAllSoTheHandlerMustReloadViaInclude();

        await _handler.Handle(new RefreshCommand("raw_incoming"), default);

        // SECURITY INVARIANT: old token revoked and chained to the new hash (auditable rotation).
        var oldPersisted = await _db.RefreshTokens.FirstAsync(rt => rt.Id == old.Id);
        oldPersisted.IsRevoked.Should().BeTrue();
        oldPersisted.ReplacedByTokenHash.Should().Be("new_refresh_hash");

        // Exactly one new active token exists with the new hash, bound to the same subject.
        var newToken = await _db.RefreshTokens.SingleAsync(rt => rt.TokenHash == "new_refresh_hash");
        newToken.IsRevoked.Should().BeFalse();
        newToken.IsActive.Should().BeTrue();
        newToken.UserId.Should().Be(_testUser.Id); // subject binding: the new token belongs to the same user
        newToken.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Handle_ShouldNeverPersistRawTokenValue()
    {
        SeedToken();
        DetachAllSoTheHandlerMustReloadViaInclude();

        await _handler.Handle(new RefreshCommand("raw_incoming"), default);

        // SECURITY: only the HASH is stored; the raw value must never touch the DB.
        var anyRaw = await _db.RefreshTokens.AnyAsync(rt => rt.TokenHash == "new_refresh_raw");
        anyRaw.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithNonExistentToken_ShouldThrowInvalidRefreshToken()
    {
        DetachAllSoTheHandlerMustReloadViaInclude();
        // No token seeded → hash not found.
        var act = async () => await _handler.Handle(new RefreshCommand("raw_incoming"), default);

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
    }

    [Fact]
    public async Task Handle_WithRevokedToken_ShouldThrowInvalidRefreshToken()
    {
        SeedToken(revoked: true);
        DetachAllSoTheHandlerMustReloadViaInclude();

        var act = async () => await _handler.Handle(new RefreshCommand("raw_incoming"), default);

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
    }

    [Fact]
    public async Task Handle_WithExpiredToken_ShouldThrowInvalidRefreshToken()
    {
        SeedToken(ttlDays: -1);
        DetachAllSoTheHandlerMustReloadViaInclude();

        var act = async () => await _handler.Handle(new RefreshCommand("raw_incoming"), default);

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
    }

    [Fact]
    public async Task Handle_ReusingAlreadyRotatedToken_ShouldThrowInvalidRefreshToken()
    {
        SeedToken();

        // First refresh rotates A → B (A becomes revoked).
        DetachAllSoTheHandlerMustReloadViaInclude();
        await _handler.Handle(new RefreshCommand("raw_incoming"), default);

        // Second refresh with the SAME raw token (A) is rejected individually via !IsActive.
        // NOTE: rejection is per-token; the handler does NOT cascade-revoke the family nor flag a breach
        // (no OWASP refresh-token reuse-detection). Tracked as a handler-hardening follow-up, not a test gap.
        DetachAllSoTheHandlerMustReloadViaInclude();
        var act = async () => await _handler.Handle(new RefreshCommand("raw_incoming"), default);
        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
    }

    [Fact]
    public async Task Handle_WithInactiveUser_ShouldThrowAndNotRotate()
    {
        var old = SeedToken();
        _testUser.Deactivate();
        await _db.SaveChangesAsync();
        DetachAllSoTheHandlerMustReloadViaInclude();

        var act = async () => await _handler.Handle(new RefreshCommand("raw_incoming"), default);
        await act.Should().ThrowAsync<AccountInactiveException>();

        // Fails closed BEFORE rotation: no new token issued and the old one is untouched.
        var count = await _db.RefreshTokens.CountAsync();
        count.Should().Be(1);
        var oldPersisted = await _db.RefreshTokens.FirstAsync(rt => rt.Id == old.Id);
        oldPersisted.IsRevoked.Should().BeFalse();
    }

    public void Dispose() => _db.Dispose();
}
