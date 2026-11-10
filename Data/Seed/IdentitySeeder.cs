using Microsoft.AspNetCore.Identity;

namespace HamiChap_MES_ERP.Data.Seed;

public static class IdentitySeeder
{
    public static async Task SeedRolesAsync(
        RoleManager<IdentityRole> roleManager)
    {
        string[] roles =
        [
            "Administrator",
            "Manager",
            "Production",
            "Warehouse",
            "Sales",
            "Accounting"
        ];

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }


    public static async Task SeedAdminAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        var adminEmail = configuration["BootstrapAdmin:Email"];
        var adminPassword = configuration["BootstrapAdmin:Password"];

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            return;

        var admin = await userManager.FindByEmailAsync(adminEmail);

        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,

                FirstName = "System",
                LastName = "Administrator",

                IsActive = true
            };

            var result = await userManager.CreateAsync(
                admin,
                adminPassword);

            if (!result.Succeeded)
                throw new InvalidOperationException("Administrator bootstrap failed.");

            var roleResult = await userManager.AddToRoleAsync(admin, "Administrator");
            if (!roleResult.Succeeded)
                throw new InvalidOperationException("Administrator role assignment failed.");
        }
    }
}