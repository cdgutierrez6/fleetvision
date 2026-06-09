using FleetVision.Identity.Application.DTOs;
using MediatR;

namespace FleetVision.Identity.Application.Auth.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<UserDto>;
