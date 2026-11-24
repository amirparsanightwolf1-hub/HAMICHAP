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
            "CEO",
            "ProductionManager",
            "FloorManager",
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
        UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        // Existing accounts are never reset or promoted automatically.
        if ((await userManager.GetUsersInRoleAsync("Administrator")).Any(x => x.IsActive))
            return;

        var email = configuration["BootstrapAdmin:Email"]?.Trim();
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "No active administrator exists. Configure BootstrapAdmin__Email and BootstrapAdmin__Password securely before first startup.");

        if (await userManager.FindByEmailAsync(email) is not null ||
            await userManager.FindByNameAsync(email) is not null)
            throw new InvalidOperationException("Bootstrap administrator already exists without an active Administrator role. Recover the account explicitly.");

        if (password.Length < 16)
            throw new InvalidOperationException("Bootstrap administrator password must contain at least 16 characters.");

        var admin = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            FirstName = "System", LastName = "Administrator", IsActive = true
        };
        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
            throw new InvalidOperationException("Administrator provisioning failed: " +
                string.Join(", ", result.Errors.Select(x => x.Code)));

        var role = await userManager.AddToRoleAsync(admin, "Administrator");
        if (!role.Succeeded)
        {
            await userManager.DeleteAsync(admin);
            throw new InvalidOperationException("Administrator role assignment failed.");
        }
    }
}
