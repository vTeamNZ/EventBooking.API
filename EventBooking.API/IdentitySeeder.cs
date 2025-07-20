using EventBooking.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace EventBooking.API
{
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Seed Roles
            string[] roleNames = { "Admin", "User", "Organizer" };
            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Seed Admin User
            var adminUser = await userManager.FindByEmailAsync("admin@kiwilanka.co.nz");
            if (adminUser == null)
            {
                var user = new ApplicationUser
                {
                    UserName = "admin@kiwilanka.co.nz",
                    Email = "admin@kiwilanka.co.nz",
                    EmailConfirmed = true,
                    FullName = "System Administrator",
                    Role = "Admin"
                };

                var result = await userManager.CreateAsync(user, "maGulak@143456");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "Admin");
                }
            }

            // Seed Test Organizer
            var organizerUser = await userManager.FindByEmailAsync("organizer@kiwilanka.co.nz");
            if (organizerUser == null)
            {
                var user = new ApplicationUser
                {
                    UserName = "organizer@kiwilanka.co.nz",
                    Email = "organizer@kiwilanka.co.nz",
                    EmailConfirmed = true,
                    FullName = "Test Organizer",
                    Role = "Organizer"
                };

                var result = await userManager.CreateAsync(user, "Organizer@123456");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, "Organizer");
                }
            }
        }
    }
}
