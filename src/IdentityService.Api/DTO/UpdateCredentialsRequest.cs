using System.ComponentModel.DataAnnotations;

namespace IdentityService.Api;

public class UpdateCredentialsRequest
{
    public string? Username { get; set; }

    [MinLength(6)]
    public string? NewPassword { get; set; }

    [Required]
    public string CurrentPassword { get; set; } = string.Empty;
}
