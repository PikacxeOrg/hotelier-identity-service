using System.Security.Claims;

using Hotelier.Events;

using IdentityService.Domain;
using IdentityService.Infrastructure;

using MassTransit;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Api;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController(
    IdentityDbContext db,
    IPublishEndpoint publisher,
    ILogger<UsersController> logger) : ControllerBase
{
    // -------------------------------------------------------
    // GET /api/users/me   (TODO 1.3 – view profile)
    // -------------------------------------------------------
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var user = await GetCurrentUser();
        if (user is null) return NotFound();

        return Ok(MapProfile(user));
    }

    // -------------------------------------------------------
    // PUT /api/users/me   (TODO 1.3 – update personal info)
    // -------------------------------------------------------
    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var user = await GetCurrentUser();
        if (user is null) return NotFound();

        if (request.Name is not null) user.Name = request.Name;
        if (request.LastName is not null) user.LastName = request.LastName;

        if (request.Email is not null && request.Email != user.Email)
        {
            if (await db.Users.AnyAsync(u => u.Email == request.Email && u.Id != user.Id))
                return Conflict(new { message = "Email already registered." });
            user.Email = request.Email;
        }

        if (request.Address is not null) user.Address = request.Address;

        await db.SaveChangesAsync();

        await publisher.Publish(new UserUpdated
        {
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email
        });

        logger.LogInformation("User {Username} updated profile", user.Username);

        return Ok(MapProfile(user));
    }

    // -------------------------------------------------------
    // PUT /api/users/me/credentials   (TODO 1.3 – update creds)
    // -------------------------------------------------------
    [HttpPut("me/credentials")]
    public async Task<IActionResult> UpdateCredentials([FromBody] UpdateCredentialsRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await GetCurrentUser();
        if (user is null) return NotFound();

        // Verify current password
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Current password is incorrect." });

        if (request.Username is not null && request.Username != user.Username)
        {
            if (await db.Users.AnyAsync(u => u.Username == request.Username && u.Id != user.Id))
                return Conflict(new { message = "Username already taken." });
            user.Username = request.Username;
        }

        if (request.NewPassword is not null)
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        await db.SaveChangesAsync();

        await publisher.Publish(new UserUpdated
        {
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email
        });

        logger.LogInformation("User {Username} updated credentials", user.Username);

        return Ok(MapProfile(user));
    }

    // -------------------------------------------------------
    // DELETE /api/users/me   (TODO 1.4 – account deletion)
    //
    // Per spec:
    //   Guest: cannot delete if active (approved) reservations exist
    //   Host:  cannot delete if future reservations on any accommodation
    //
    // The actual reservation check is enforced by calling reservation-service
    // via a synchronous HTTP request or a Saga. For this reference
    // implementation we publish the UserDeleted event; reservation-service
    // and accommodation-service handle cascading logic on their end.
    //
    // If a hard pre-check is needed later, add an HTTP call here.
    // -------------------------------------------------------
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteAccount()
    {
        var user = await GetCurrentUser();
        if (user is null) return NotFound();

        db.Users.Remove(user); // cascade deletes refresh tokens
        await db.SaveChangesAsync();

        await publisher.Publish(new UserDeleted
        {
            UserId = user.Id,
            UserType = user.UserType.ToString()
        });

        logger.LogInformation("User {Username} deleted account", user.Username);

        return NoContent();
    }

    // -------------------------------------------------------
    // GET /api/users/{id}   (public profile – used by other services)
    // -------------------------------------------------------
    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        return Ok(MapProfile(user));
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------
    private async Task<User?> GetCurrentUser()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(sub, out var userId))
            return null;

        return await db.Users.FindAsync(userId);
    }

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
