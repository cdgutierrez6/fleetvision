using FleetVision.Identity.Application.DTOs;
using MediatR;

namespace FleetVision.Identity.Application.Auth.Queries.Login;

public sealed record LoginQuery(string Email, string Password) : IRequest<TokenResponse>;
