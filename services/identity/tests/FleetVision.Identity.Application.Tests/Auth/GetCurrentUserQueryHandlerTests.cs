using FleetVision.Identity.Application.Auth.Queries.GetCurrentUser;
using FleetVision.Identity.Domain.Entities;
using FleetVision.Identity.Domain.Enums;
using FleetVision.Identity.Domain.Exceptions;
using FleetVision.Identity.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.Identity.Application.Tests.Auth;

public sealed class GetCurrentUserQueryHandlerTests : IDisposable
{
    private readonly IdentityDbContext _db;
    private readonly GetCurrentUserQueryHandler _handler;
    private readonly User _testUser;

    public GetCurrentUserQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new IdentityDbContext(options);

        _testUser = User.Create(Guid.NewGuid(), "user@test.com", "argon2_hash", "Juan", "Gomez", UserRole.Admin);
        _db.Users.Add(_testUser);
        _db.SaveChanges();

        _handler = new GetCurrentUserQueryHandler(_db);
    }

    [Fact]
    public async Task Handle_WithExistingUser_ShouldMapAllFields()
    {
        var result = await _handler.Handle(new GetCurrentUserQuery(_testUser.Id), default);

        result.Id.Should().Be(_testUser.Id);
        result.TenantId.Should().Be(_testUser.TenantId);
        result.Email.Should().Be(_testUser.Email);
        result.FirstName.Should().Be(_testUser.FirstName);
        result.LastName.Should().Be(_testUser.LastName);
        result.Role.Should().Be(UserRole.Admin.ToString());
        result.IsActive.Should().BeTrue();
        result.CreatedAt.Should().Be(_testUser.CreatedAt);
        result.LastLoginAt.Should().Be(_testUser.LastLoginAt);
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ShouldThrowUserNotFound()
    {
        var act = async () => await _handler.Handle(new GetCurrentUserQuery(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task Handle_WithInactiveUser_ShouldReturnDtoWithoutFiltering()
    {
        _testUser.Deactivate();
        await _db.SaveChangesAsync();

        // Contract difference vs Login/Refresh: GetCurrentUser does NOT filter by account state.
        var result = await _handler.Handle(new GetCurrentUserQuery(_testUser.Id), default);

        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldNotMutateState_ReadOnly()
    {
        var usersBefore = await _db.Users.CountAsync();
        var tokensBefore = await _db.RefreshTokens.CountAsync();

        await _handler.Handle(new GetCurrentUserQuery(_testUser.Id), default);

        (await _db.Users.CountAsync()).Should().Be(usersBefore);
        (await _db.RefreshTokens.CountAsync()).Should().Be(tokensBefore);
    }

    public void Dispose() => _db.Dispose();
}
