namespace Hotelier.Events;

/// <summary>
/// Published when a new user registers.
/// </summary>
public record UserRegistered
{
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string UserType { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
