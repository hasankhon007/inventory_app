using Microsoft.AspNetCore.Identity;

namespace Course.Models;

public class ApplicationUser : IdentityUser
{
    public string? PreferredTheme { get; set; }
    public string? PreferredCulture { get; set; }
    public bool IsBlocked { get; set; }
}
