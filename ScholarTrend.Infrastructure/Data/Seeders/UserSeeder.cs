using Microsoft.AspNetCore.Identity;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Domain.Enums;

namespace ScholarTrend.Infrastructure.Data.Seeders;

public static class UserSeeder
{
    public static async Task SeedAsync(UserManager<User> userManager)
    {
        var users = new[]
        {
            new User
            {
                UserName = "admin@gmail.com",
                Email = "admin@gmail.com",
                FullName = "System Administrator",
                Institution = "ScholarTrend University",
                ResearchField = "System Administration",
                EmailConfirmed = true,
                IsActive = true
            },
            new User
            {
                UserName = "thuan@gmail.com",
                Email = "thuan@gmail.com",
                FullName = "Dr. Thuan Nguyen",
                Institution = "National University",
                ResearchField = "Artificial Intelligence",
                EmailConfirmed = true,
                IsActive = true
            },
            new User
            {
                UserName = "tien@gmail.com",
                Email = "tien@gmail.com",
                FullName = "Dr. Tien Tran",
                Institution = "Institute of Technology",
                ResearchField = "Data Science",
                EmailConfirmed = true,
                IsActive = true
            },
            new User
            {
                UserName = "lan@gmail.com",
                Email = "lan@gmail.com",
                FullName = "Lan Pham",
                Institution = "ScholarTrend University",
                ResearchField = "Computer Science",
                EmailConfirmed = true,
                IsActive = true
            },
            new User
            {
                UserName = "nam@gmail.com",
                Email = "nam@gmail.com",
                FullName = "Nam Le",
                Institution = "ScholarTrend University",
                ResearchField = "Information Systems",
                EmailConfirmed = true,
                IsActive = true
            }
        };

        var passwords = new Dictionary<string, string>
        {
            ["admin@gmail.com"] = "Admin123!",
            ["thuan@gmail.com"] = "Thuan123!",
            ["tien@gmail.com"] = "Tien123!",
            ["lan@gmail.com"] = "Lan123!",
            ["nam@gmail.com"] = "Nam123!"
        };

        foreach (var user in users)
        {
            var existingUser = await userManager.FindByEmailAsync(user.Email!);
            if (existingUser != null)
            {
                continue;
            }

            var createResult = await userManager.CreateAsync(user, passwords[user.Email!]);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Failed to create user '{user.Email}': {errors}");
            }

            var roleName = user.Email == "admin@gmail.com"
                ? UserRole.Admin.ToString()
                : user.Email!.Contains("researcher", StringComparison.OrdinalIgnoreCase)
                    ? UserRole.Researcher.ToString()
                    : UserRole.LecturerStudent.ToString();

            if (!await userManager.IsInRoleAsync(user, roleName))
            {
                await userManager.AddToRoleAsync(user, roleName);
            }
        }
    }
}
