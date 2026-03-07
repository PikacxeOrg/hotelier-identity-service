namespace Hotelier.Events;

/// <summary>
/// Published when a user updates their profile.
/// </summary>
public record UserUpdated
{
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
