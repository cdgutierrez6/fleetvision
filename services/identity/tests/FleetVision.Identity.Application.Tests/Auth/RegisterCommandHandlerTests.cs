using FleetVision.Identity.Application.Auth.Commands.Register;
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

public sealed class RegisterCommandHandlerTests : IDisposable
{
    private readonly IdentityDbContext _db;
    private readonly Mock<IPasswordHasher> _hasherMock;
    private readonly Mock<ITokenService> _tokenMock;
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new IdentityDbContext(options);
        _hasherMock = new Mock<IPasswordHasher>();
        _tokenMock = new Mock<ITokenService>();
        var loggerMock = new Mock<ILogger<RegisterCommandHandler>>();

        _hasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed_password");
        _tokenMock.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("access_token");
        _tokenMock.Setup(t => t.GenerateRefreshToken()).Returns("refresh_token_raw");
        _tokenMock.Setup(t => t.HashToken("refresh_token_raw")).Returns("refresh_token_hash");

        _handler = new RegisterCommandHandler(_db, _hasherMock.Object, _tokenMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnTokenResponse()
    {
        var command = new RegisterCommand("Acme Corp", "admin@acme.com", "Secure123!", "Juan", "Gomez");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.AccessToken.Should().Be("access_token");
        result.RefreshToken.Should().Be("refresh_token_raw");
        result.ExpiresIn.Should().Be(900);
        result.TokenType.Should().Be("Bearer");
    }

    [Fact]
    public async Task Handle_ShouldPersistTenantAndUser()
    {
        var command = new RegisterCommand("Acme Corp", "admin@acme.com", "Secure123!", "Juan", "Gomez");

        await _handler.Handle(command, CancellationToken.None);

        var tenant = await _db.Tenants.FirstOrDefaultAsync();
        var user = await _db.Users.FirstOrDefaultAsync();

        tenant.Should().NotBeNull();
        tenant!.Name.Should().Be("Acme Corp");

        user.Should().NotBeNull();
        user!.Email.Should().Be("admin@acme.com");
        user.Role.Should().Be(UserRole.Admin);
        user.TenantId.Should().Be(tenant.Id);
    }

    [Fact]
    public async Task Handle_ShouldCreateRefreshToken()
    {
        var command = new RegisterCommand("Acme Corp", "admin@acme.com", "Secure123!", "Juan", "Gomez");

        await _handler.Handle(command, CancellationToken.None);

        var refreshToken = await _db.RefreshTokens.FirstOrDefaultAsync();
        refreshToken.Should().NotBeNull();
        refreshToken!.TokenHash.Should().Be("refresh_token_hash");
        refreshToken.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithExistingEmail_ShouldThrowDuplicateEmailException()
    {
        // Seed existing user
        var existingTenant = Tenant.Create("Existing Corp", "existing");
        var existingUser = User.Create(existingTenant.Id, "admin@acme.com", "hash", "A", "B", UserRole.Admin);
        _db.Tenants.Add(existingTenant);
        _db.Users.Add(existingUser);
        await _db.SaveChangesAsync();

        var command = new RegisterCommand("New Corp", "admin@acme.com", "Secure123!", "Ana", "Lopez");

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateEmailException>();
    }

    [Fact]
    public async Task Handle_ShouldNormalizeEmailToLowercase()
    {
        var command = new RegisterCommand("Acme Corp", "ADMIN@ACME.COM", "Secure123!", "Juan", "Gomez");

        await _handler.Handle(command, CancellationToken.None);

        var user = await _db.Users.FirstOrDefaultAsync();
        user!.Email.Should().Be("admin@acme.com");
    }

    [Fact]
    public async Task Handle_ShouldHashPassword()
    {
        var command = new RegisterCommand("Acme Corp", "admin@acme.com", "Secure123!", "Juan", "Gomez");

        await _handler.Handle(command, CancellationToken.None);

        _hasherMock.Verify(h => h.Hash("Secure123!"), Times.Once);
        var user = await _db.Users.FirstOrDefaultAsync();
        user!.PasswordHash.Should().Be("hashed_password");
    }

    public void Dispose() => _db.Dispose();
}
