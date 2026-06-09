using FleetVision.Identity.Application.Common.Interfaces;
using FleetVision.Identity.Application.DTOs;
using FleetVision.Identity.Domain.Entities;
using FleetVision.Identity.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetVision.Identity.Application.Auth.Commands.Refresh;

public sealed class RefreshCommandHandler : IRequestHandler<RefreshCommand, TokenResponse>
{
    private readonly IIdentityDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly ILogger<RefreshCommandHandler> _logger;

    public RefreshCommandHandler(
        IIdentityDbContext db,
        ITokenService tokenService,
        ILogger<RefreshCommandHandler> logger)
    {
        _db = db;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<TokenResponse> Handle(RefreshCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = _tokenService.HashToken(request.RefreshToken);

        var storedToken = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null || !storedToken.IsActive)
            throw new InvalidRefreshTokenException();

        if (!storedToken.User.IsActive)
            throw new AccountInactiveException();

        var rawNewRefreshToken = _tokenService.GenerateRefreshToken();
        var newTokenHash = _tokenService.HashToken(rawNewRefreshToken);

        // Rotation: revoke old token and issue new one
        storedToken.Revoke(replacedByHash: newTokenHash);

        var newRefreshToken = RefreshToken.Create(
            userId: storedToken.UserId,
            tokenHash: newTokenHash,
            ttlDays: 30);

        _db.RefreshTokens.Add(newRefreshToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Refresh token rotated for user {UserId}", storedToken.UserId);

        var accessToken = _tokenService.GenerateAccessToken(storedToken.User);

        return new TokenResponse(accessToken, rawNewRefreshToken, 900);
    }
}
