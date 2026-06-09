using MediatR;

namespace FleetVision.Identity.Application.Auth.Commands.Logout;

public sealed record LogoutCommand(string RefreshToken) : IRequest;
