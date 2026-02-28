using System.ComponentModel.DataAnnotations;

namespace IdentityService.Domain;

public enum UserType
{
    Guest,
    Host
}

public class User : TrackableEntity
{
    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    [Required]
    public UserType UserType { get; set; }

    /// <summary>
    /// Per-notification-type preferences (e.g. "ReservationCreated" -> true/false).
    /// Stored as a JSON column.
    /// </summary>
    public Dictionary<string, bool> NotificationPreferences { get; set; } = new();
}
