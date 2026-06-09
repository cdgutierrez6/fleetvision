using FleetVision.Identity.Application.DTOs;
using MediatR;

namespace FleetVision.Identity.Application.Auth.Commands.UpdateProfile;

public sealed record UpdateProfileCommand(Guid UserId, string FirstName, string LastName) : IRequest<UserDto>;
