namespace Hotelier.Events;

/// <summary>
/// Published when a user deletes their account.
/// Consumed by accommodation-service (cascade-delete host accommodations),
/// reservation-service (cancel active reservations).
/// </summary>
public record UserDeleted
{
    public Guid UserId { get; init; }
    public string UserType { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
