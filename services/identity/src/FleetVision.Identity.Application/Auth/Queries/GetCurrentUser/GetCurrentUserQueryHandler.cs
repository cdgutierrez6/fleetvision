using FleetVision.Identity.Application.Common.Interfaces;
using FleetVision.Identity.Application.DTOs;
using FleetVision.Identity.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.Identity.Application.Auth.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, UserDto>
{
    private readonly IIdentityDbContext _db;

    public GetCurrentUserQueryHandler(IIdentityDbContext db) => _db = db;

    public async Task<UserDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new UserNotFoundException(request.UserId);

        return new UserDto(
            user.Id,
            user.TenantId,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role.ToString(),
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt);
    }
}
