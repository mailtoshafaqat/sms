using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SMS.Tests.Integration;

public static class QaTestAuth
{
    public const string Scheme = "QaTest";
    public const string RoleHeader = "X-Qa-Role";
    public const string UserIdHeader = "X-Qa-UserId";

    public static WebApplicationFactory<Program> WithQaAuthentication(this WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = Scheme;
                        options.DefaultChallengeScheme = Scheme;
                    })
                    .AddScheme<AuthenticationSchemeOptions, QaTestAuthHandler>(Scheme, _ => { });
            });
        });

    public static HttpClient CreateRoleClient(this WebApplicationFactory<Program> factory, string role, string userId)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        client.DefaultRequestHeaders.Add(RoleHeader, role);
        client.DefaultRequestHeaders.Add(UserIdHeader, userId);
        return client;
    }
}

internal sealed class QaTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(QaTestAuth.RoleHeader, out var roleValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var role = roleValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = Request.Headers[QaTestAuth.UserIdHeader].FirstOrDefault() ?? "qa-user";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, $"qa-{role.ToLowerInvariant()}@test.local"),
            new(ClaimTypes.Email, $"qa-{role.ToLowerInvariant()}@test.local"),
            new(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, QaTestAuth.Scheme);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, QaTestAuth.Scheme)));
    }
}
