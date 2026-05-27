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

        // Seed a sample inventory if none exist
        if (!await dbContext.Inventories.AnyAsync())
        {
            var adminId = adminUser.Id;
            var categoryId = dbContext.InventoryCategories.First().Id;
            
            var inventoryId = Guid.NewGuid();
            var inventory = new Inventory
            {
                Id = inventoryId,
                Title = "IT Equipment",
                Description = "Laptops, monitors, and other IT gear.",
                CategoryId = categoryId,
                OwnerId = adminId,
                IsPublic = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Inventories.Add(inventory);

            var field1 = new CustomField { Id = Guid.NewGuid(), InventoryId = inventoryId, Name = "Serial Number", FieldType = Course.Domain.Enums.InventoryFieldType.SingleLineText, IsRequired = true, DisplayOrder = 1, CreatedAt = DateTimeOffset.UtcNow };
            var field2 = new CustomField { Id = Guid.NewGuid(), InventoryId = inventoryId, Name = "Notes", FieldType = Course.Domain.Enums.InventoryFieldType.MultiLineText, DisplayOrder = 2, CreatedAt = DateTimeOffset.UtcNow };
            var field3 = new CustomField { Id = Guid.NewGuid(), InventoryId = inventoryId, Name = "Weight (kg)", FieldType = Course.Domain.Enums.InventoryFieldType.Number, SettingsJson = "{\"MinValue\":0,\"MaxValue\":10000}", DisplayOrder = 3, CreatedAt = DateTimeOffset.UtcNow };
            var field4 = new CustomField { Id = Guid.NewGuid(), InventoryId = inventoryId, Name = "In Stock", FieldType = Course.Domain.Enums.InventoryFieldType.Boolean, DisplayOrder = 4, CreatedAt = DateTimeOffset.UtcNow };
            var field5 = new CustomField { Id = Guid.NewGuid(), InventoryId = inventoryId, Name = "Manual URL", FieldType = Course.Domain.Enums.InventoryFieldType.Url, DisplayOrder = 5, CreatedAt = DateTimeOffset.UtcNow };
            var field6 = new CustomField { Id = Guid.NewGuid(), InventoryId = inventoryId, Name = "Condition", FieldType = Course.Domain.Enums.InventoryFieldType.OneFromList, SettingsJson = "{\"Options\":[\"New\",\"Used\",\"Refurbished\"]}", DisplayOrder = 6, CreatedAt = DateTimeOffset.UtcNow };

            dbContext.CustomFields.AddRange(field1, field2, field3, field4, field5, field6);

            var item1 = new Item
            {
                Id = Guid.NewGuid(),
                InventoryId = inventoryId,
                CustomId = "IT-001",
                Name = "MacBook Pro 16",
                Description = "M3 Max, 64GB RAM",
                Price = 3499.00m,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedById = adminId,
                UpdatedById = adminId
            };

            var item1Values = new[]
            {
                new ItemFieldValue { Id = Guid.NewGuid(), ItemId = item1.Id, CustomFieldId = field1.Id, Value = "C02F9X" },
                new ItemFieldValue { Id = Guid.NewGuid(), ItemId = item1.Id, CustomFieldId = field2.Id, Value = "Assigned to engineering" },
                new ItemFieldValue { Id = Guid.NewGuid(), ItemId = item1.Id, CustomFieldId = field3.Id, Value = "2.1" },
                new ItemFieldValue { Id = Guid.NewGuid(), ItemId = item1.Id, CustomFieldId = field4.Id, Value = "false" },
                new ItemFieldValue { Id = Guid.NewGuid(), ItemId = item1.Id, CustomFieldId = field5.Id, Value = "https://support.apple.com/manuals/macbookpro" },
                new ItemFieldValue { Id = Guid.NewGuid(), ItemId = item1.Id, CustomFieldId = field6.Id, Value = "New" },
            };

            dbContext.Items.Add(item1);
            dbContext.ItemFieldValues.AddRange(item1Values);

            // Create a regular user
            var regularEmail = "user@course.com";
            var regularUser = await userManager.FindByEmailAsync(regularEmail);
            if (regularUser == null)
            {
                regularUser = new ApplicationUser
                {
                    UserName = regularEmail,
                    Email = regularEmail,
                    EmailConfirmed = true,
                    FullName = "Standard User",
                    PreferredTheme = "light",
                    PreferredCulture = "en",
                    IsBlocked = false
                };

                var createResult = await userManager.CreateAsync(regularUser, "UserPassword123!");
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(regularUser, "User");
                    logger.LogInformation("Seeded default standard user: {RegularEmail}", regularEmail);
                }
                else
                {
                    logger.LogError("Failed to seed default user: {Errors}", string.Join(", ", createResult.Errors.Select(e => e.Description)));
                }
            }

            var regularUserId = regularUser?.Id ?? adminId;
            var otherCategoryId = dbContext.InventoryCategories.Skip(1).First().Id;

            // Seed second inventory for regular user
            var inventory2Id = Guid.NewGuid();
            var inventory2 = new Inventory
            {
                Id = inventory2Id,
                Title = "Office Furniture",
                Description = "Desks, chairs, and filing cabinets.",
                CategoryId = otherCategoryId,
                OwnerId = regularUserId,
                IsPublic = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Inventories.Add(inventory2);

            var inv2Field1 = new CustomField { Id = Guid.NewGuid(), InventoryId = inventory2Id, Name = "Material", FieldType = Course.Domain.Enums.InventoryFieldType.SingleLineText, IsRequired = true, DisplayOrder = 1, CreatedAt = DateTimeOffset.UtcNow };
            var inv2Field2 = new CustomField { Id = Guid.NewGuid(), InventoryId = inventory2Id, Name = "Assembly Required", FieldType = Course.Domain.Enums.InventoryFieldType.Boolean, DisplayOrder = 2, CreatedAt = DateTimeOffset.UtcNow };
            var inv2Field3 = new CustomField { Id = Guid.NewGuid(), InventoryId = inventory2Id, Name = "Color", FieldType = Course.Domain.Enums.InventoryFieldType.OneFromList, SettingsJson = "{\"Options\":[\"Black\",\"White\",\"Wood\",\"Gray\"]}", DisplayOrder = 3, CreatedAt = DateTimeOffset.UtcNow };
            
            dbContext.CustomFields.AddRange(inv2Field1, inv2Field2, inv2Field3);

            var item2 = new Item
            {
                Id = Guid.NewGuid(),
                InventoryId = inventory2Id,
                CustomId = "FURN-001",
                Name = "Ergonomic Office Chair",
                Description = "Mesh back, adjustable lumbar support",
                Price = 299.50m,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedById = regularUserId,
                UpdatedById = regularUserId
            };

            var item2Values = new[]
            {
                new ItemFieldValue { Id = Guid.NewGuid(), ItemId = item2.Id, CustomFieldId = inv2Field1.Id, Value = "Mesh/Plastic" },
                new ItemFieldValue { Id = Guid.NewGuid(), ItemId = item2.Id, CustomFieldId = inv2Field2.Id, Value = "true" },
                new ItemFieldValue { Id = Guid.NewGuid(), ItemId = item2.Id, CustomFieldId = inv2Field3.Id, Value = "Black" }
            };

            dbContext.Items.Add(item2);
            dbContext.ItemFieldValues.AddRange(item2Values);

            await dbContext.SaveChangesAsync();
            logger.LogInformation("Seeded sample inventories and items.");
        }
    }
}