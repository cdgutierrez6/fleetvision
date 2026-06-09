using FleetVision.Identity.Application.DTOs;
using FleetVision.Identity.Domain.Entities;

namespace FleetVision.Identity.Application.Common.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    string HashToken(string token);
    Guid? GetUserIdFromToken(string accessToken);
}
