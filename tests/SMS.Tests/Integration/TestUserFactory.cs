using Microsoft.AspNetCore.Identity;
using SMS.Domain.Common;
using SMS.Infrastructure.Identity;

namespace SMS.Tests.Integration;

internal static class TestUserFactory
{
    public static async Task EnsureGateKeeperRoleAsync(RoleManager<IdentityRole> roleManager)
    {
        if (!await roleManager.RoleExistsAsync(RoleNames.GateKeeper))
        {
            await roleManager.CreateAsync(new IdentityRole(RoleNames.GateKeeper));
        }
    }

    public static async Task<ApplicationUser> EnsureGateKeeperUserAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        await EnsureGateKeeperRoleAsync(roleManager);

        var existing = await userManager.FindByEmailAsync("gatekeeper@school.local");
        if (existing is not null)
        {
            return existing;
        }

        var gateKeeper = new ApplicationUser
        {
            UserName = "gatekeeper@school.local",
            Email = "gatekeeper@school.local",
            EmailConfirmed = true,
            DisplayName = "QA Gate Keeper",
            IsActive = true
        };

        var result = await userManager.CreateAsync(gateKeeper, "GateKeeper@123");
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(gateKeeper, RoleNames.GateKeeper);
        return gateKeeper;
    }

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
