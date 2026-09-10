using FleetVision.Identity.Application.Auth.Commands.UpdateProfile;
using FleetVision.Identity.Domain.Entities;
using FleetVision.Identity.Domain.Enums;
using FleetVision.Identity.Domain.Exceptions;
using FleetVision.Identity.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.Identity.Application.Tests.Auth;

public sealed class UpdateProfileCommandHandlerTests : IDisposable
{
    private readonly IdentityDbContext _db;
    private readonly UpdateProfileCommandHandler _handler;
    private readonly User _testUser;

    public UpdateProfileCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new IdentityDbContext(options);

        _testUser = User.Create(Guid.NewGuid(), "user@test.com", "argon2_hash", "Juan", "Gomez", UserRole.Admin);
        _db.Users.Add(_testUser);
        _db.SaveChanges();

        _handler = new UpdateProfileCommandHandler(_db);
    }

    [Fact]
    public async Task Handle_WithExistingUser_ShouldReturnUpdatedDto()
    {
        var result = await _handler.Handle(
            new UpdateProfileCommand(_testUser.Id, "Ana", "Lopez"), default);

        result.FirstName.Should().Be("Ana");
        result.LastName.Should().Be("Lopez");
        result.Id.Should().Be(_testUser.Id);
        result.Role.Should().Be(UserRole.Admin.ToString());
    }

    [Fact]
    public async Task Handle_ShouldPersistNewNames()
    {
        await _handler.Handle(new UpdateProfileCommand(_testUser.Id, "Ana", "Lopez"), default);

        var reloaded = await _db.Users.FirstAsync(u => u.Id == _testUser.Id);
        reloaded.FirstName.Should().Be("Ana");
        reloaded.LastName.Should().Be("Lopez");
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ShouldThrowUserNotFound()
    {
        var act = async () => await _handler.Handle(
            new UpdateProfileCommand(Guid.NewGuid(), "Ana", "Lopez"), default);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldTrimNames_EntityInvariant()
    {
        await _handler.Handle(new UpdateProfileCommand(_testUser.Id, "  Ana  ", "  Lopez  "), default);

        var reloaded = await _db.Users.FirstAsync(u => u.Id == _testUser.Id);
        reloaded.FirstName.Should().Be("Ana");
        reloaded.LastName.Should().Be("Lopez");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WithBlankFirstName_ShouldThrowArgumentException(string firstName)
    {
        // The handler has NO validator; the only guard is the entity's ThrowIfNullOrWhiteSpace.
        var act = async () => await _handler.Handle(
            new UpdateProfileCommand(_testUser.Id, firstName, "Lopez"), default);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WithBlankLastName_ShouldThrowArgumentException(string lastName)
    {
        var act = async () => await _handler.Handle(
            new UpdateProfileCommand(_testUser.Id, "Ana", lastName), default);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_ShouldMapAllOtherFieldsUnchanged()
    {
        var result = await _handler.Handle(
            new UpdateProfileCommand(_testUser.Id, "Ana", "Lopez"), default);

        result.Email.Should().Be(_testUser.Email);
        result.TenantId.Should().Be(_testUser.TenantId);
        result.IsActive.Should().Be(_testUser.IsActive);
        result.CreatedAt.Should().Be(_testUser.CreatedAt);
        result.LastLoginAt.Should().Be(_testUser.LastLoginAt);
    }

    public void Dispose() => _db.Dispose();
}
