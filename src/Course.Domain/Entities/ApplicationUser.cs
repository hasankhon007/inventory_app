using Microsoft.AspNetCore.Identity;

namespace Course.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string? PreferredTheme { get; set; }
    public string? PreferredCulture { get; set; }
    public bool IsBlocked { get; set; }
    public string? FullName { get; set; }
}
