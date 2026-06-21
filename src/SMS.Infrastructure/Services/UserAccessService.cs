using Microsoft.AspNetCore.Identity;
using SMS.Application.Interfaces;
using SMS.Application.Interfaces.Repositories;
using SMS.Infrastructure.Identity;

namespace SMS.Infrastructure.Services;

public class UserAccessService(
    UserManager<ApplicationUser> userManager,
    IClassRepository classRepository) : IUserAccessService
{
    public async Task<bool> IsAdminAsync(string? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var user = await userManager.FindByIdAsync(userId);
        return user is not null && await userManager.IsInRoleAsync(user, "Admin");
    }

    public async Task<bool> IsCoordinatorAsync(string? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var user = await userManager.FindByIdAsync(userId);
        return user is not null && await userManager.IsInRoleAsync(user, "Coordinator");
    }

    public async Task<bool> HasFullAttendanceAccessAsync(string? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return true;
        }

        return await IsAdminAsync(userId, cancellationToken)
            || await IsCoordinatorAsync(userId, cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetAllowedSectionIdsAsync(string? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        if (await HasFullAttendanceAccessAsync(userId, cancellationToken))
        {
            var sections = await classRepository.GetActiveSectionsAsync(cancellationToken);
            return sections.Select(x => x.Id).ToList();
        }

        return await classRepository.GetSectionIdsForTeacherUserAsync(userId, cancellationToken);
    }
}
