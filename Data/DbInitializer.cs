using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;

namespace AcxiomCRM.Data;

public static class DbInitializer
{
    public const string RoleAdmin = "Admin";
    public const string RoleManager = "Manager";
    public const string RoleSalesExecutive = "SalesExecutive";

    public static readonly string[] Roles = [RoleAdmin, RoleManager, RoleSalesExecutive];

    public static async Task SeedAsync(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        // Ensure database is created and migrations are applied
        await context.Database.MigrateAsync();

        // 1. Seed Roles
        foreach (var roleName in Roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (roleResult.Succeeded)
                {
                    logger.LogInformation("Role '{RoleName}' created successfully.", roleName);
                }
                else
                {
                    logger.LogError("Failed to create role '{RoleName}': {Errors}", 
                        roleName, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                }
            }
        }

        // 2. Safe Development Admin Seed Mechanism from Configuration
        var adminEmail = configuration["SeedData:AdminUser:Email"] ?? "admin@acxiomcrm.local";
        var adminFullName = configuration["SeedData:AdminUser:FullName"] ?? "System Administrator";
        var adminPassword = configuration["SeedData:AdminUser:Password"];

        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
            if (existingAdmin == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = adminFullName,
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };

                var createResult = await userManager.CreateAsync(adminUser, adminPassword);
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, RoleAdmin);
                    logger.LogInformation("Default Admin user '{AdminEmail}' seeded and assigned to '{RoleAdmin}' role.", adminEmail, RoleAdmin);

                    // Add initial audit log
                    context.AuditLogs.Add(new AuditLog
                    {
                        UserId = adminUser.Id,
                        Action = "SystemSetup",
                        EntityName = "ApplicationUser",
                        EntityId = adminUser.Id,
                        Details = "System administrator account initialized via secure seed configuration.",
                        Timestamp = DateTime.UtcNow
                    });
                    await context.SaveChangesAsync();
                }
                else
                {
                    logger.LogWarning("Failed to seed admin user '{AdminEmail}': {Errors}",
                        adminEmail, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                }
            }
        }
    }
}
