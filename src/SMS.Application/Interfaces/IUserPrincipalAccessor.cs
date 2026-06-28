using System.Security.Claims;

namespace SMS.Application.Interfaces;

public interface IUserPrincipalAccessor
{
    Task<ClaimsPrincipal?> GetPrincipalAsync(CancellationToken cancellationToken = default);
}
