using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models.Constants;
using Staybnb.Models.Entities;
using Staybnb.Models.Enums;
using Staybnb.Services.Interfaces;

namespace Staybnb.Data.Seeders
{
    /// <summary>
    /// Runs once at startup (called from Program.cs):
    ///   1. Applies any pending EF Core migrations (creates the database on a fresh machine).
    ///   2. Creates the four roles: Guest, Host, Admin, SuperAdmin.
    ///   3. Creates the SuperAdmin account from the "SuperAdmin" section of appsettings.json.
    /// Safe to run every time - it only creates what is missing.
    /// </summary>
    public static class RoleSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            // Controllers get a new "scope" per request automatically; at startup we create one ourselves.
            using var scope = services.CreateScope();
            var provider = scope.ServiceProvider;

            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("RoleSeeder");
            var context = provider.GetRequiredService<ApplicationDbContext>();
            var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
            var configuration = provider.GetRequiredService<IConfiguration>();
            var activityLog = provider.GetRequiredService<IActivityLogService>();

            // ---------- 1. Make sure the database exists and is up to date ----------
            // Without a migration, MigrateAsync only creates an EMPTY database and the
            // role lookup below fails with "Invalid object name 'AspNetRoles'".
            if (!context.Database.GetMigrations().Any())
            {
                throw new InvalidOperationException(
                    "No EF Core migrations were found in this project. Open Tools > NuGet Package Manager > " +
                    "Package Manager Console and run:  Add-Migration InitialCreate  then  Update-Database");
            }

            await context.Database.MigrateAsync();

            // ---------- 2. Roles ----------
            foreach (var roleName in Roles.All)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                    if (result.Succeeded)
                        logger.LogInformation("Created role {Role}", roleName);
                    else
                        logger.LogError("Could not create role {Role}: {Errors}", roleName, JoinErrors(result));
                }
            }

            // ---------- 3. SuperAdmin user ----------
            var section = configuration.GetSection("SuperAdmin");
            var email = section["Email"] ?? "superadmin@staybnb.co.za";
            var password = section["Password"] ?? "SuperAdmin@123";
            var firstName = section["FirstName"] ?? "Super";
            var lastName = section["LastName"] ?? "Admin";

            var superAdmin = await userManager.FindByEmailAsync(email);
            if (superAdmin == null)
            {
                superAdmin = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = firstName,
                    LastName = lastName,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await userManager.CreateAsync(superAdmin, password);
                if (!createResult.Succeeded)
                {
                    logger.LogError("Could not create SuperAdmin: {Errors}", JoinErrors(createResult));
                    return;
                }

                logger.LogInformation("Created SuperAdmin account {Email}", email);
                await activityLog.LogAsync(superAdmin.Id, ActivityActions.SystemSeeded, ActivityType.System,
                    $"SuperAdmin account {email} created on startup.");
            }

            // Make sure the account has the SuperAdmin role (and only that role)
            if (!await userManager.IsInRoleAsync(superAdmin, Roles.SuperAdmin))
            {
                await userManager.AddToRoleAsync(superAdmin, Roles.SuperAdmin);
                await activityLog.LogAsync(superAdmin.Id, ActivityActions.RoleAssigned, ActivityType.RoleChange,
                    $"Role '{Roles.SuperAdmin}' assigned to {email} by the system seeder.");
            }

            foreach (var otherRole in new[] { Roles.Guest, Roles.Host })
            {
                if (await userManager.IsInRoleAsync(superAdmin, otherRole))
                    await userManager.RemoveFromRoleAsync(superAdmin, otherRole);
            }
        }

        private static string JoinErrors(IdentityResult result) =>
            string.Join("; ", result.Errors.Select(e => e.Description));
    }
}
