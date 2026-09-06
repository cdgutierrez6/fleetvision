using FleetVision.Identity.Application.Common.Interfaces;
using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using System.Text;

namespace FleetVision.Identity.Infrastructure.Services;

public sealed class Argon2PasswordHasher : IPasswordHasher
{
    // Argon2id parameters — OWASP recommended minimums (2024)
    private const int MemorySize = 65536;  // 64 MB
    private const int Iterations = 3;
    private const int DegreeOfParallelism = 4;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    // Computed exactly once (process-wide, not per DI instance). Reuses Hash() so the
    // dummy inherits the same m/t/p cost parameters as real hashes — single source of
    // truth, no duplicated constants. The random password guarantees Verify(anything,
    // DummyHash) is false, so it can never authenticate anyone.
    private static readonly Lazy<string> LazyDummyHash = new(
        () => new Argon2PasswordHasher().Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));

    /// <inheritdoc />
    public string DummyHash => LazyDummyHash.Value;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = MemorySize,
            Iterations = Iterations,
            DegreeOfParallelism = DegreeOfParallelism
        };

        var hash = argon2.GetBytes(HashSize);

        // Format: $argon2id$v=19$m={mem},t={iter},p={par}${base64salt}${base64hash}
        return $"$argon2id$v=19$m={MemorySize},t={Iterations},p={DegreeOfParallelism}" +
               $"${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hash)
    {
        try
        {
            if (!hash.StartsWith("$argon2id$"))
                return false;

            // A well-formed hash yields exactly 5 segments: argon2id, v=19, params, salt, hash.
            // Guard on < 5 (not < 4) so a malformed 4-segment string is rejected here explicitly
            // instead of throwing IndexOutOfRange at parts[4] and relying on the catch below.
            var parts = hash.Split('$', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
                return false;

            // parts[2] = "m=65536,t=3,p=4"
            var paramsParts = parts[2].Split(',');
            var mem = int.Parse(paramsParts[0].Replace("m=", ""));
            var iter = int.Parse(paramsParts[1].Replace("t=", ""));
            var par = int.Parse(paramsParts[2].Replace("p=", ""));

            var salt = Convert.FromBase64String(parts[3]);
            var expectedHash = Convert.FromBase64String(parts[4]);

            using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                MemorySize = mem,
                Iterations = iter,
                DegreeOfParallelism = par
            };

            var actualHash = argon2.GetBytes(expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }
}
