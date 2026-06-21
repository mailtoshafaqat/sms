using Microsoft.AspNetCore.Identity;
using SMS.Infrastructure.Identity;

namespace SMS.Tests.Integration;

internal static class TestUserFactory
{
    public static async Task<ApplicationUser> EnsureTeacherUserAsync(UserManager<ApplicationUser> userManager)
    {
        var existing = await userManager.FindByEmailAsync("teacher@school.local");
        if (existing is not null)
        {
            return existing;
        }

        var teacher = new ApplicationUser
        {
            UserName = "teacher@school.local",
            Email = "teacher@school.local",
            EmailConfirmed = true,
            DisplayName = "QA Teacher",
            IsActive = true
        };

        var result = await userManager.CreateAsync(teacher, "Teacher@123");
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(teacher, "Teacher");
        return teacher;
    }
}
