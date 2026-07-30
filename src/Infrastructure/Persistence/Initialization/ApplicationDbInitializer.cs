using Care.WebApi.Domain.Care;
using Care.WebApi.Infrastructure.Identity;
using Care.WebApi.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Care.WebApi.Infrastructure.Persistence.Initialization;

public static class ApplicationDbInitializer
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        if ((await context.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            logger.LogInformation("Applying pending migrations.");
            await context.Database.MigrateAsync(cancellationToken);
        }

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (string roleName in new[] { "Admin", "Member" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
                logger.LogInformation("Seeded role {RoleName}.", roleName);
            }
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (!await userManager.Users.AnyAsync(cancellationToken))
        {
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            string email = config["SecuritySettings:DefaultAdmin:Email"] ?? "admin@example.com";
            string password = config["SecuritySettings:DefaultAdmin:Password"] ?? "ChangeMe123!";

            var admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                Name = "Admin",
                Status = UserStatus.Active,
                EmailConfirmed = true,
                InvitedAt = DateTime.UtcNow,
                JoinedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(admin, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
                logger.LogInformation("Seeded default admin user {Email} — log in and change the password immediately.", email);
            }
            else
            {
                logger.LogError("Failed to seed default admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        if (!await context.ShiftTemplates.AnyAsync(cancellationToken))
        {
            var templates = new List<ShiftTemplate>();
            foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
            {
                templates.Add(new ShiftTemplate { DayOfWeek = day, ShiftType = ShiftType.Day, StartTime = new TimeSpan(7, 0, 0), EndTime = new TimeSpan(15, 0, 0) });
                templates.Add(new ShiftTemplate { DayOfWeek = day, ShiftType = ShiftType.Evening, StartTime = new TimeSpan(15, 0, 0), EndTime = new TimeSpan(23, 0, 0) });
                templates.Add(new ShiftTemplate { DayOfWeek = day, ShiftType = ShiftType.Overnight, StartTime = new TimeSpan(23, 0, 0), EndTime = new TimeSpan(7, 0, 0) });
            }

            context.ShiftTemplates.AddRange(templates);
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} default shift templates.", templates.Count);
        }
    }
}
