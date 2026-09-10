using FleetVision.Identity.Infrastructure.Services;
using FluentAssertions;

namespace FleetVision.Identity.Infrastructure.Tests.Services;

/// <summary>
/// Guards the fragile invariant behind the login timing-attack defense (REGLA #9):
/// <see cref="Argon2PasswordHasher.DummyHash"/> must be a real, valid Argon2id hash
/// carrying the same cost parameters as <see cref="Argon2PasswordHasher.Hash"/>, so the
/// "user not found" branch of login runs a KDF of identical cost. If anyone regresses it
/// to a malformed literal, or drifts the parameters of Hash without updating the dummy,
/// these tests go red.
/// </summary>
public sealed class Argon2PasswordHasherDummyHashTests
{
    private readonly Argon2PasswordHasher _hasher = new();

    [Fact]
    public void DummyHash_ShouldBeStructurallyValidArgon2idHashWithFiveParts()
    {
        var dummy = _hasher.DummyHash;

        dummy.Should().StartWith("$argon2id$");

        // RemoveEmptyEntries is exactly how Verify() tokenizes the hash. A malformed
        // literal like "$argon2id$v=19$m=...$invalid" collapses to 4 parts and never
        // reaches the KDF; a real hash yields exactly 5: [algo, version, params, salt, hash].
        var parts = dummy.Split('$', StringSplitOptions.RemoveEmptyEntries);
        parts.Should().HaveCount(5);

        parts[0].Should().Be("argon2id");
        parts[1].Should().Be("v=19");

        // parts[3] = base64 salt (16 bytes), parts[4] = base64 hash (32 bytes).
        var salt = Convert.FromBase64String(parts[3]);
        var hash = Convert.FromBase64String(parts[4]);
        salt.Should().HaveCount(16);
        hash.Should().HaveCount(32);
    }

    [Fact]
    public void DummyHash_ShouldCarrySameCostParametersAsRealHash()
    {
        // The heart of the fix: identical m/t/p means identical KDF cost on both login
        // branches. If Hash()'s parameters ever change and DummyHash's don't (or vice
        // versa), this assertion fails and surfaces the timing regression.
        var realParams = ParamsSegment(_hasher.Hash("any-password"));
        var dummyParams = ParamsSegment(_hasher.DummyHash);

        dummyParams.Should().Be(realParams);
        dummyParams.Should().Be("m=65536,t=3,p=4");
    }

    [Fact]
    public void Verify_WithAnyPassword_AgainstDummyHash_ShouldReturnFalse()
    {
        // DummyHash is derived from a random password, so it must never authenticate.
        _hasher.Verify("", _hasher.DummyHash).Should().BeFalse();
        _hasher.Verify("password123", _hasher.DummyHash).Should().BeFalse();
        _hasher.Verify("Secure123!", _hasher.DummyHash).Should().BeFalse();
    }

    [Fact]
    public void DummyHash_ShouldBeStableAcrossReads()
    {
        // Computed once via Lazy — no recompute (and no cost jitter) per login.
        _hasher.DummyHash.Should().Be(_hasher.DummyHash);
    }

    [Fact]
    public void DummyHash_ShouldBeSharedAcrossInstances()
    {
        // Static Lazy: DI creating a new hasher instance must not pay the KDF cost again.
        new Argon2PasswordHasher().DummyHash.Should().Be(new Argon2PasswordHasher().DummyHash);
    }

    [Fact]
    public void Verify_WithCorrectPassword_ShouldReturnTrue()
    {
        // No-regression: the additive change did not break normal verification.
        const string password = "Secure123!";
        _hasher.Verify(password, _hasher.Hash(password)).Should().BeTrue();
    }

    [Fact]
    public void Verify_WithWrongPassword_ShouldReturnFalse()
    {
        _hasher.Verify("wrong-password", _hasher.Hash("Secure123!")).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]                                                // empty
    [InlineData("not-a-hash")]                                      // no $argon2id$ prefix
    [InlineData("$argon2id$")]                                      // prefix only, 1 segment
    [InlineData("$argon2id$v=19$m=65536,t=3,p=4$invalid")]          // the exact malformed literal this fix removed (4 parts, bad base64)
    [InlineData("$argon2id$v=19$m=65536,t=3,p=4$onlyfourparts")]    // 4 segments — no hash segment, rejected by the length guard
    [InlineData("$argon2id$v=19$m=65536,t=3,p=4$c2FsdA==$!!!bad")]  // 5 segments but broken base64 hash — exercises the try/catch
    public void Verify_WithMalformedHash_ShouldReturnFalseNotThrow(string malformed)
    {
        // The whole fix hinges on Verify degrading safely: a malformed hash must return
        // false, NEVER throw. This locks in the guard + try/catch path so a future refactor
        // that turns malformed input into an unhandled exception is caught by the build.
        _hasher.Verify("whatever", malformed).Should().BeFalse();
    }

    private static string ParamsSegment(string argon2Hash)
        => argon2Hash.Split('$', StringSplitOptions.RemoveEmptyEntries)[2];
}
