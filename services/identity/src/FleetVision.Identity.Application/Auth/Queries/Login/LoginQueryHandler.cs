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

        // Timing-safe: always hash even if user not found to prevent enumeration
        var dummyHash = "$argon2id$v=19$m=65536,t=3,p=4$invalid";
        var hashToVerify = user?.PasswordHash ?? dummyHash;
        var passwordValid = _passwordHasher.Verify(request.Password, hashToVerify);

        if (user is null || !passwordValid)
            throw new InvalidCredentialsException();

        if (!user.IsActive)
            throw new AccountInactiveException();

        // Revoke all existing refresh tokens for this user (single session per user)
        var existingTokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == user.Id && !rt.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var oldToken in existingTokens)
            oldToken.Revoke();

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
