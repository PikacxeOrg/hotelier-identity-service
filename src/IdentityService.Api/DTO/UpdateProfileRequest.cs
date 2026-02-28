using System.ComponentModel.DataAnnotations;

namespace IdentityService.Api;

public class UpdateProfileRequest
{
    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    public string? Address { get; set; }
}
