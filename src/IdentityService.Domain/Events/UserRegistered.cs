namespace IdentityService.Domain;

/// <summary>
/// Published when a new user registers. Consumed by other services
/// that need to know about new users (e.g. notification-service).
/// </summary>
public record UserRegistered
{
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public UserType UserType { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
