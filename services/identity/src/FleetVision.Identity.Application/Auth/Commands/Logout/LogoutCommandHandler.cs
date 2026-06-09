using FleetVision.Identity.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetVision.Identity.Application.Auth.Commands.Logout;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IIdentityDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly ILogger<LogoutCommandHandler> _logger;

    public LogoutCommandHandler(
        IIdentityDbContext db,
        ITokenService tokenService,
        ILogger<LogoutCommandHandler> logger)
    {
        _db = db;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = _tokenService.HashToken(request.RefreshToken);

        var storedToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null || storedToken.IsRevoked)
            return; // Idempotente: no error si ya está revocado

        storedToken.Revoke();
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} logged out", storedToken.UserId);
    }
}
