namespace IdentityService.Domain;

/// <summary>
/// Published when a user deletes their account.
/// Consumed by accommodation-service (to remove host's accommodations),
/// reservation-service (to handle active reservations), etc.
/// </summary>
public record UserDeleted
{
    public Guid UserId { get; init; }
    public UserType UserType { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
