using FleetVision.Identity.Application.DTOs;
using MediatR;

namespace FleetVision.Identity.Application.Auth.Commands.Register;

public sealed record RegisterCommand(
    string CompanyName,
    string AdminEmail,
    string AdminPassword,
    string AdminFirstName,
    string AdminLastName) : IRequest<TokenResponse>;
