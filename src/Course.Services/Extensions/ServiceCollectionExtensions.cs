using Course.DataAccess.Contexts;
using Course.DataAccess.Repositories;
using Course.DataAccess.UnitOfWork;
using Course.Domain.Entities;
using Course.Services.Implementations;
using Course.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Course.Services.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCourseServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Connection string and DbContext
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        // 2. Identity registration
        services.AddDefaultIdentity<ApplicationUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.Password.RequireDigit = false;
            options.Password.RequiredLength = 6;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>();

        // 3. Register Abstractions & Generic Repository Pattern
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));

        // 4. Register Services
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IInventoryItemService, InventoryItemService>();
        services.AddScoped<IInventoryAccessService, InventoryAccessService>();
        services.AddScoped<IInventoryFieldService, InventoryFieldService>();
        services.AddScoped<IInventoryTagService, InventoryTagService>();
        services.AddScoped<IInventorySearchService, InventorySearchService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IInventoryCreateValidator, InventoryCreateValidator>();
        services.AddScoped<IAdminService, AdminService>();

        return services;
    }
}
