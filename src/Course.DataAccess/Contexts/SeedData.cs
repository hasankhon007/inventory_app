using Course.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Course.DataAccess.Contexts;

public static class SeedData
{
    public static async Task EnsureSeededAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("SeedData");

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            await dbContext.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Skipping database migration/seed because the database is unavailable.");
            return;
        }

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Seed roles
        var roles = new[] { "Admin", "User" };
        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // Seed admin user
        var adminEmail = "admin@course.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "System Administrator",
                PreferredTheme = "dark",
                PreferredCulture = "en",
                IsBlocked = false
            };

            var createResult = await userManager.CreateAsync(adminUser, "AdminPassword123!");
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger.LogInformation("Seeded default admin user: {AdminEmail}", adminEmail);
            }
            else
            {
                logger.LogError("Failed to seed default admin: {Errors}", string.Join(", ", createResult.Errors.Select(e => e.Description)));
            }
        }

        var defaultCategories = new[] { "Equipment", "Furniture", "Book", "Other" };
        var existingNames = await dbContext.InventoryCategories
            .Select(category => category.Name)
            .ToListAsync();

        var missingCategories = defaultCategories
            .Where(name => !existingNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Select(name => new InventoryCategory { Name = name })
            .ToList();

        if (missingCategories.Count == 0)
        {
            return;
        }

        dbContext.InventoryCategories.AddRange(missingCategories);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded {CategoryCount} inventory categories.", missingCategories.Count);
    }
}