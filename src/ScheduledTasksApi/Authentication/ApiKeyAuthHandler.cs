#if !WINDOWS
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ScheduledTasksApi.Authentication;

public class ApiKeyAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredKey = configuration.GetValue<string>("ApiKey");

        // If no API key configured, allow anonymous access
        if (string.IsNullOrEmpty(configuredKey))
        {
            var anonIdentity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "Anonymous") }, "ApiKey");
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(anonIdentity), "ApiKey")));
        }

        if (!Request.Headers.TryGetValue("X-Api-Key", out var providedKey))
            return Task.FromResult(AuthenticateResult.Fail("Missing X-Api-Key header"));

        if (providedKey != configuredKey)
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "ApiKeyUser") }, "ApiKey");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "ApiKey");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
#endif
