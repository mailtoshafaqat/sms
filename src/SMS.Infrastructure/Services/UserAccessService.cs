using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using SMS.Application.Interfaces;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Common;
using SMS.Infrastructure.Identity;

namespace SMS.Infrastructure.Services;

public class UserAccessService(
    IUserPrincipalAccessor principalAccessor,
    UserManager<ApplicationUser> userManager,
    IClassRepository classRepository) : IUserAccessService
{
    private readonly SemaphoreSlim identityGate = new(1, 1);
    private string? cachedUserId;
    private IReadOnlySet<string>? cachedRoles;

    public Task<bool> IsAdminAsync(string? userId, CancellationToken cancellationToken = default) =>
        CheckRoleAsync(userId, RoleNames.Admin, cancellationToken);

    public Task<bool> IsCoordinatorAsync(string? userId, CancellationToken cancellationToken = default) =>
        CheckRoleAsync(userId, RoleNames.Coordinator, cancellationToken);

    public Task<bool> IsGateKeeperAsync(string? userId, CancellationToken cancellationToken = default) =>
        CheckRoleAsync(userId, RoleNames.GateKeeper, cancellationToken);

    public async Task<bool> IsGateKeeperOnlyAsync(string? userId, CancellationToken cancellationToken = default)
    {
        if (!await CheckRoleAsync(userId, RoleNames.GateKeeper, cancellationToken))
        {
            return false;
        }

        if (await CheckRoleAsync(userId, RoleNames.Admin, cancellationToken)
            || await CheckRoleAsync(userId, RoleNames.Coordinator, cancellationToken)
            || await CheckRoleAsync(userId, RoleNames.Teacher, cancellationToken))
        {
            return false;
        }

        return true;
    }

    public async Task<bool> HasFullAttendanceAccessAsync(string? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return true;
        }

        return await CheckRoleAsync(userId, RoleNames.Admin, cancellationToken)
            || await CheckRoleAsync(userId, RoleNames.Coordinator, cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetAllowedSectionIdsAsync(string? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        if (await CheckRoleAsync(userId, RoleNames.Admin, cancellationToken)
            || await CheckRoleAsync(userId, RoleNames.Coordinator, cancellationToken)
            || await CheckRoleAsync(userId, RoleNames.GateKeeper, cancellationToken))
        {
            var sections = await classRepository.GetActiveSectionsAsync(cancellationToken);
            return sections.Select(x => x.Id).ToList();
        }

        return await classRepository.GetSectionIdsForTeacherUserAsync(userId, cancellationToken);
    }

    private async Task<bool> CheckRoleAsync(string? userId, string role, CancellationToken cancellationToken)
    {
        if (await IsInRoleForUserAsync(userId, role, cancellationToken))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var principal = await principalAccessor.GetPrincipalAsync(cancellationToken);
        if (principal?.Identity?.IsAuthenticated == true)
        {
            return false;
        }

        var roles = await GetRolesFromIdentityAsync(userId, cancellationToken);
        return roles.Contains(role);
    }

    private async Task<bool> IsInRoleForUserAsync(string? userId, string role, CancellationToken cancellationToken)
    {
        var principal = await GetPrincipalForUserAsync(userId, cancellationToken);
        return principal?.IsInRole(role) == true;
    }

    private async Task<ClaimsPrincipal?> GetPrincipalForUserAsync(string? userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var principal = await principalAccessor.GetPrincipalAsync(cancellationToken);
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return principal.FindFirstValue(ClaimTypes.NameIdentifier) == userId
            ? principal
            : null;
    }

    private async Task<IReadOnlySet<string>> GetRolesFromIdentityAsync(string userId, CancellationToken cancellationToken)
    {
        if (cachedUserId == userId && cachedRoles is not null)
        {
            return cachedRoles;
        }

        await identityGate.WaitAsync(cancellationToken);
        try
        {
            if (cachedUserId == userId && cachedRoles is not null)
            {
                return cachedRoles;
            }

            var user = await userManager.FindByIdAsync(userId);
            if (user is null)
            {
                cachedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                var roles = await userManager.GetRolesAsync(user);
                cachedRoles = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            cachedUserId = userId;
            return cachedRoles;
        }
        finally
        {
            identityGate.Release();
        }
    }
}
