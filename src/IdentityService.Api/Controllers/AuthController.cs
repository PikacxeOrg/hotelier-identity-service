using System.Security.Claims;

using IdentityService.Domain;
using IdentityService.Infrastructure;

using MassTransit;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Api;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    IdentityDbContext db,
    IJwtTokenService jwt,
    IPublishEndpoint publisher,
    ILogger<AuthController> logger) : ControllerBase
{
    // -------------------------------------------------------
    // POST /api/auth/register   (TODO 1.1)
    // -------------------------------------------------------
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Unique username check
        if (await db.Users.AnyAsync(u => u.Username == request.Username))
            return Conflict(new { message = "Username already taken." });

        // Unique email check
        if (await db.Users.AnyAsync(u => u.Email == request.Email))
            return Conflict(new { message = "Email already registered." });

        var user = new User
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Name = request.Name,
            LastName = request.LastName,
            Email = request.Email,
            Address = request.Address,
            UserType = request.UserType
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Generate tokens
        var accessToken = jwt.GenerateAccessToken(user);
        var refreshToken = jwt.GenerateRefreshToken(user);

        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();

        // Publish event
        await publisher.Publish(new UserRegistered
        {
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            UserType = user.UserType
        });

        logger.LogInformation("User {Username} registered (type={UserType})", user.Username, user.UserType);

        return CreatedAtAction(nameof(Register), new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            ExpiresAt = refreshToken.ExpiresAt,
            User = MapProfile(user)
        });
    }

    // -------------------------------------------------------
    // POST /api/auth/login   (TODO 1.2)
    // -------------------------------------------------------
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid username or password." });

        // Generate tokens
        var accessToken = jwt.GenerateAccessToken(user);
        var refreshToken = jwt.GenerateRefreshToken(user);

        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();

        logger.LogInformation("User {Username} logged in", user.Username);

        return Ok(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            ExpiresAt = refreshToken.ExpiresAt,
            User = MapProfile(user)
        });
    }

    // -------------------------------------------------------
    // POST /api/auth/refresh
    // -------------------------------------------------------
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        // Validate the expired access token to extract the user ID
        var principal = jwt.ValidateExpiredToken(request.AccessToken);
        if (principal is null)
            return Unauthorized(new { message = "Invalid access token." });

        var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? principal.FindFirstValue("sub");
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Invalid token claims." });

        // Look up the stored refresh token
        var storedToken = await db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt =>
                rt.Token == request.RefreshToken
                && rt.UserId == userId
                && !rt.IsRevoked);

        if (storedToken is null || storedToken.ExpiresAt < DateTime.UtcNow)
            return Unauthorized(new { message = "Invalid or expired refresh token." });

        // Revoke old, issue new
        storedToken.IsRevoked = true;
        var newRefreshToken = jwt.GenerateRefreshToken(storedToken.User);
        db.RefreshTokens.Add(newRefreshToken);
        await db.SaveChangesAsync();

        var newAccessToken = jwt.GenerateAccessToken(storedToken.User);

        return Ok(new AuthResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken.Token,
            ExpiresAt = newRefreshToken.ExpiresAt,
            User = MapProfile(storedToken.User)
        });
    }

    // -------------------------------------------------------
    // Helper
    // -------------------------------------------------------
    private static UserProfile MapProfile(User u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Name = u.Name,
        LastName = u.LastName,
        Email = u.Email,
        Address = u.Address,
        UserType = u.UserType,
        NotificationPreferences = u.NotificationPreferences
    };
}
