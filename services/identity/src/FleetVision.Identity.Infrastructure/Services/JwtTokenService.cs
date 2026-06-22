using FleetVision.Identity.Application.Common.Interfaces;
using FleetVision.Identity.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace FleetVision.Identity.Infrastructure.Services;

public sealed class JwtTokenService : ITokenService
{
    private readonly SigningCredentials _signingCredentials;
    private readonly IReadOnlyList<SecurityKey> _validationKeys;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenTtlMinutes;

    public JwtTokenService(IConfiguration configuration)
    {
        _issuer               = configuration["Jwt:Issuer"]    ?? "fleetvision-identity";
        _audience             = configuration["Jwt:Audience"]  ?? "fleetvision-api";
        _accessTokenTtlMinutes = int.TryParse(
            configuration["Jwt:AccessTokenTtlMinutes"], out var ttl) ? ttl : 15;

        // RSA private key — used only in Identity to sign tokens
        var privateKeyPem = LoadKeyMaterial(
            configuration, "Jwt:RsaPrivateKey", "Jwt:RsaPrivateKeyPath",
            "Jwt:RsaPrivateKey or Jwt:RsaPrivateKeyPath is required for the Identity service.");
        var rsaPrivate = RSA.Create();
        rsaPrivate.ImportFromPem(privateKeyPem.AsSpan());
        _signingCredentials = new SigningCredentials(
            new RsaSecurityKey(rsaPrivate) { KeyId = "rsa-1" },
            SecurityAlgorithms.RsaSha256);

        // RSA public key — used to validate tokens in GetUserIdFromToken
        var publicKeyPem = LoadKeyMaterial(
            configuration, "Jwt:RsaPublicKey", "Jwt:RsaPublicKeyPath",
            "Jwt:RsaPublicKey or Jwt:RsaPublicKeyPath is required.");
        var rsaPublic = RSA.Create();
        rsaPublic.ImportFromPem(publicKeyPem.AsSpan());
        var validationKeys = new List<SecurityKey>
        {
            new RsaSecurityKey(rsaPublic) { KeyId = "rsa-1" }
        };

        // Legacy HS256 key — remove Jwt:SigningKey from config once all tokens have rotated
        var legacyKey = configuration["Jwt:SigningKey"];
        if (!string.IsNullOrWhiteSpace(legacyKey))
            validationKeys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(legacyKey)));

        _validationKeys = validationKeys.AsReadOnly();
    }

    public string GenerateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new("tenant_id",                   user.TenantId?.ToString() ?? string.Empty),
            new(ClaimTypes.Role,               user.Role.ToString()),
            new("first_name",                  user.FirstName),
            new("last_name",                   user.LastName)
        };

        var token = new JwtSecurityToken(
            issuer:            _issuer,
            audience:          _audience,
            claims:            claims,
            expires:           DateTime.UtcNow.AddMinutes(_accessTokenTtlMinutes),
            signingCredentials: _signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashToken(string token)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public Guid? GetUserIdFromToken(string accessToken, bool allowExpired = false)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();

            handler.ValidateToken(accessToken, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys        = _validationKeys,
                ValidateIssuer           = true,
                ValidIssuer              = _issuer,
                ValidateAudience         = true,
                ValidAudience            = _audience,
                ValidateLifetime         = !allowExpired,
            }, out var validatedToken);

            var jwt = (JwtSecurityToken)validatedToken;
            var sub = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;

            return Guid.TryParse(sub, out var userId) ? userId : null;
        }
        catch
        {
            return null;
        }
    }

    private static string LoadKeyMaterial(
        IConfiguration config, string envKey, string pathKey, string error)
    {
        var pem = config[envKey];
        if (!string.IsNullOrWhiteSpace(pem)) return pem;

        var path = config[pathKey];
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            return File.ReadAllText(path);

        throw new InvalidOperationException(error);
    }
}
