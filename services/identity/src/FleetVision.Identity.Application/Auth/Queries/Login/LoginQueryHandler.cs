using FleetVision.Identity.Application.Common.Interfaces;
using FleetVision.Identity.Application.DTOs;
using FleetVision.Identity.Domain.Entities;
using FleetVision.Identity.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetVision.Identity.Application.Auth.Queries.Login;

public sealed class LoginQueryHandler : IRequestHandler<LoginQuery, TokenResponse>
{
    private readonly IIdentityDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ILogger<LoginQueryHandler> _logger;

    public LoginQueryHandler(
        IIdentityDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ILogger<LoginQueryHandler> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<TokenResponse> Handle(LoginQuery request, CancellationToken cancellationToken)
    {
        var email = request.Email.ToLowerInvariant().Trim();

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Against user enumeration: when the user does not exist we verify against
        // DummyHash — a real, valid Argon2id hash with the same cost parameters — so both
        // branches run the full KDF. This equalises the DOMINANT cost (~tens of ms) and
        // closes the enumeration oracle; verifying against a malformed literal would fail
        // in microseconds and leak, via timing, which emails exist. A second-order residual
        // remains (the indexed Users lookup differs for a hit vs a miss), orders of
        // magnitude below the KDF and accepted here.
        var hashToVerify = user?.PasswordHash ?? _passwordHasher.DummyHash;
        var passwordValid = _passwordHasher.Verify(request.Password, hashToVerify);

        if (user is null || !passwordValid)
            throw new InvalidCredentialsException();

        if (!user.IsActive)
            throw new AccountInactiveException();

        var rawRefreshToken = _tokenService.GenerateRefreshToken();
        var refreshToken = RefreshToken.Create(
            userId: user.Id,
            tokenHash: _tokenService.HashToken(rawRefreshToken),
            ttlDays: 30);

        user.UpdateLastLogin();
        _db.RefreshTokens.Add(refreshToken);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} logged in", user.Id);

        var accessToken = _tokenService.GenerateAccessToken(user);

        return new TokenResponse(accessToken, rawRefreshToken, 900);
    }
}
