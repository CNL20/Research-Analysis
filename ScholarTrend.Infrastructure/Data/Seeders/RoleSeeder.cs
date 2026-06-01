using Microsoft.AspNetCore.Identity;
using ScholarTrend.Domain.Enums;

namespace ScholarTrend.Infrastructure.Data.Seeders;

public static class RoleSeeder
{
    public static async Task SeedAsync(RoleManager<IdentityRole> roleManager)
    {
        string[] roles =
        [
            UserRole.Admin.ToString(),
            UserRole.Researcher.ToString(),
            UserRole.LecturerStudent.ToString()
        ];

        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
    }
}
