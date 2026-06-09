using FleetVision.Identity.Application.DTOs;
using MediatR;

namespace FleetVision.Identity.Application.Auth.Commands.Refresh;

public sealed record RefreshCommand(string RefreshToken) : IRequest<TokenResponse>;
