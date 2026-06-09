using FleetVision.Identity.Application.Auth.Queries.Login;
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

public sealed class LoginQueryHandlerTests : IDisposable
{
    private readonly IdentityDbContext _db;
    private readonly Mock<IPasswordHasher> _hasherMock;
    private readonly Mock<ITokenService> _tokenMock;
    private readonly LoginQueryHandler _handler;
    private readonly User _testUser;

    public LoginQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new IdentityDbContext(options);
        _hasherMock = new Mock<IPasswordHasher>();
        _tokenMock = new Mock<ITokenService>();
        var loggerMock = new Mock<ILogger<LoginQueryHandler>>();

        _testUser = User.Create(Guid.NewGuid(), "user@test.com", "argon2_hash", "Juan", "Gomez", UserRole.Admin);
        _db.Users.Add(_testUser);
        _db.SaveChanges();

        _hasherMock.Setup(h => h.Verify("Secure123!", "argon2_hash")).Returns(true);
        _hasherMock.Setup(h => h.Verify(It.IsNotIn("Secure123!"), It.IsAny<string>())).Returns(false);
        _hasherMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsNotIn("argon2_hash"))).Returns(false);

        _tokenMock.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("access_token");
        _tokenMock.Setup(t => t.GenerateRefreshToken()).Returns("new_refresh_raw");
        _tokenMock.Setup(t => t.HashToken("new_refresh_raw")).Returns("new_refresh_hash");

        _handler = new LoginQueryHandler(_db, _hasherMock.Object, _tokenMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ShouldReturnTokens()
    {
        var result = await _handler.Handle(new LoginQuery("user@test.com", "Secure123!"), default);

        result.AccessToken.Should().Be("access_token");
        result.RefreshToken.Should().Be("new_refresh_raw");
        result.ExpiresIn.Should().Be(900);
    }

    [Fact]
    public async Task Handle_ShouldPersistNewRefreshToken()
    {
        await _handler.Handle(new LoginQuery("user@test.com", "Secure123!"), default);

        var token = await _db.RefreshTokens.FirstOrDefaultAsync();
        token.Should().NotBeNull();
        token!.TokenHash.Should().Be("new_refresh_hash");
        token.UserId.Should().Be(_testUser.Id);
    }

    [Fact]
    public async Task Handle_WithWrongPassword_ShouldThrowInvalidCredentials()
    {
        var act = async () => await _handler.Handle(new LoginQuery("user@test.com", "WrongPassword"), default);
        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Handle_WithNonExistentEmail_ShouldThrowInvalidCredentials()
    {
        // Timing-safe: the handler always verifies password even for non-existent users
        var act = async () => await _handler.Handle(new LoginQuery("nobody@test.com", "Secure123!"), default);
        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Handle_WithInactiveUser_ShouldThrowAccountInactiveException()
    {
        _testUser.Deactivate();
        await _db.SaveChangesAsync();

        var act = async () => await _handler.Handle(new LoginQuery("user@test.com", "Secure123!"), default);
        await act.Should().ThrowAsync<AccountInactiveException>();
    }

    [Fact]
    public async Task Handle_ShouldNormalizeEmailBeforeSearch()
    {
        var result = await _handler.Handle(new LoginQuery("USER@TEST.COM", "Secure123!"), default);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ShouldUpdateLastLoginAt()
    {
        await _handler.Handle(new LoginQuery("user@test.com", "Secure123!"), default);

        var user = await _db.Users.FirstAsync();
        user.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    public void Dispose() => _db.Dispose();
}
