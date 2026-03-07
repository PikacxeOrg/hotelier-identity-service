using IdentityService.Domain;

namespace IdentityService.Api;

public class UserProfile
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public UserType UserType { get; set; }
    public Dictionary<string, bool> NotificationPreferences { get; set; } = new();
}
