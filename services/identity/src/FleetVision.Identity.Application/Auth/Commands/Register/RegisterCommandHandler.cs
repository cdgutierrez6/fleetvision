using FleetVision.Identity.Application.Common.Interfaces;
using FleetVision.Identity.Application.DTOs;
using FleetVision.Identity.Domain.Entities;
using FleetVision.Identity.Domain.Enums;
using FleetVision.Identity.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetVision.Identity.Application.Auth.Commands.Register;

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, TokenResponse>
{
    private readonly IIdentityDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IIdentityDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ILogger<RegisterCommandHandler> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<TokenResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.AdminEmail.ToLowerInvariant().Trim();

        var emailExists = await _db.Users
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (emailExists)
            throw new DuplicateEmailException();

        var slug = GenerateSlug(request.CompanyName);

        var slugExists = await _db.Tenants
            .AnyAsync(t => t.Slug == slug, cancellationToken);

        if (slugExists)
            throw new DuplicateTenantSlugException(slug);

        var tenant = Tenant.Create(request.CompanyName, slug);

        var passwordHash = _passwordHasher.Hash(request.AdminPassword);
        var adminUser = User.Create(
            tenantId: tenant.Id,
            email: normalizedEmail,
            passwordHash: passwordHash,
            firstName: request.AdminFirstName,
            lastName: request.AdminLastName,
            role: UserRole.Admin);

        var rawRefreshToken = _tokenService.GenerateRefreshToken();
        var refreshToken = RefreshToken.Create(
            userId: adminUser.Id,
            tokenHash: _tokenService.HashToken(rawRefreshToken),
            ttlDays: 30);

        adminUser.UpdateLastLogin();

        _db.Tenants.Add(tenant);
        _db.Users.Add(adminUser);
        _db.RefreshTokens.Add(refreshToken);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tenant {TenantId} registered with admin user {UserId}", tenant.Id, adminUser.Id);

        var accessToken = _tokenService.GenerateAccessToken(adminUser);

        return new TokenResponse(accessToken, rawRefreshToken, 900);
    }

    private static string GenerateSlug(string companyName)
    {
        var slug = companyName
            .ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("&", "and");

        var allowedChars = slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray();
        return new string(allowedChars).Trim('-');
    }
}
