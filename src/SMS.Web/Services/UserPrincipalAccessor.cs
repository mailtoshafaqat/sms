using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using SMS.Application.Interfaces;

namespace SMS.Web.Services;

public class UserPrincipalAccessor(
    IHttpContextAccessor httpContextAccessor,
    AuthenticationStateProvider authenticationStateProvider) : IUserPrincipalAccessor
{
    private ClaimsPrincipal? cachedPrincipal;

    public async Task<ClaimsPrincipal?> GetPrincipalAsync(CancellationToken cancellationToken = default)
    {
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser?.Identity?.IsAuthenticated == true)
        {
            return httpUser;
        }

        if (cachedPrincipal?.Identity?.IsAuthenticated == true)
        {
            return cachedPrincipal;
        }

        // Blazor auth state is only valid inside an HTTP request / component scope.
        if (httpContextAccessor.HttpContext is null)
        {
            return null;
        }

        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        cachedPrincipal = authState.User;
        return cachedPrincipal?.Identity?.IsAuthenticated == true ? cachedPrincipal : null;
    }
}
