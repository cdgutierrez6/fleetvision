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

            var parts = hash.Split('$', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
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
