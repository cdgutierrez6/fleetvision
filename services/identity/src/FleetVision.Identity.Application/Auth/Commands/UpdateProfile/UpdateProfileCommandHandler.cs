using FleetVision.Identity.Application.Common.Interfaces;
using FleetVision.Identity.Application.DTOs;
using FleetVision.Identity.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FleetVision.Identity.Application.Auth.Commands.UpdateProfile;

public sealed class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, UserDto>
{
    private readonly IIdentityDbContext _db;

    public UpdateProfileCommandHandler(IIdentityDbContext db) => _db = db;

    public async Task<UserDto> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new UserNotFoundException(request.UserId);

        user.UpdateProfile(request.FirstName, request.LastName);
        await _db.SaveChangesAsync(cancellationToken);

        return new UserDto(user.Id, user.TenantId, user.Email, user.FirstName, user.LastName,
            user.Role.ToString(), user.IsActive, user.CreatedAt, user.LastLoginAt);
    }
}
