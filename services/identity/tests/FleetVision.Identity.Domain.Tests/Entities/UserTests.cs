using FleetVision.Identity.Domain.Entities;
using FleetVision.Identity.Domain.Enums;
using FluentAssertions;

namespace FleetVision.Identity.Domain.Tests.Entities;

public sealed class UserTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateUser()
    {
        var tenantId = Guid.NewGuid();
        var user = User.Create(tenantId, "user@test.com", "hash123", "Juan", "Gomez", UserRole.Admin);

        user.Id.Should().NotBeEmpty();
        user.TenantId.Should().Be(tenantId);
        user.Email.Should().Be("user@test.com");
        user.Role.Should().Be(UserRole.Admin);
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_SuperAdmin_ShouldHaveNullTenantId()
    {
        var user = User.Create(null, "superadmin@fv.com", "hash", "Super", "Admin", UserRole.SuperAdmin);
        user.TenantId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldNormalizeEmailToLowercase()
    {
        var user = User.Create(null, "USER@TEST.COM", "hash", "A", "B", UserRole.Admin);
        user.Email.Should().Be("user@test.com");
    }

    [Fact]
    public void UpdateLastLogin_ShouldSetLastLoginAt()
    {
        var user = User.Create(null, "u@t.com", "hash", "A", "B", UserRole.Viewer);
        user.LastLoginAt.Should().BeNull();

        user.UpdateLastLogin();

        user.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateProfile_ShouldChangeName()
    {
        var user = User.Create(null, "u@t.com", "hash", "Juan", "Gomez", UserRole.Admin);
        user.UpdateProfile("Maria", "Lopez");

        user.FirstName.Should().Be("Maria");
        user.LastName.Should().Be("Lopez");
        user.FullName.Should().Be("Maria Lopez");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateProfile_WithEmptyFirstName_ShouldThrow(string name)
    {
        var user = User.Create(null, "u@t.com", "hash", "A", "B", UserRole.Admin);
        var act = () => user.UpdateProfile(name, "Valid");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveToFalse()
    {
        var user = User.Create(null, "u@t.com", "hash", "A", "B", UserRole.Admin);
        user.Deactivate();
        user.IsActive.Should().BeFalse();
    }
}
