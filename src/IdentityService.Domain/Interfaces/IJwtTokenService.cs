using System.Security.Claims;

namespace IdentityService.Domain;

public interface IJwtTokenService
{
    string GenerateAccessToken(User user);
    RefreshToken GenerateRefreshToken(User user);
    ClaimsPrincipal? ValidateExpiredToken(string token);
}
