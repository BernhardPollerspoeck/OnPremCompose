using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DeploymentRunner.Services;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IAuthService _authService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IAuthService authService)
        : base(options, logger, encoder)
    {
        _authService = authService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return AuthenticateResult.Fail("Missing Authorization Header");
        }

        var apiKey = authHeader.ToString().Replace("Bearer ", "").Trim();

        if (string.IsNullOrEmpty(apiKey))
        {
            return AuthenticateResult.Fail("Invalid Authorization Header");
        }

        var isValid = await _authService.ValidateApiKeyAsync(apiKey);

        if (!isValid)
        {
            return AuthenticateResult.Fail("Invalid API Key");
        }

        // Update last used timestamp (fire and forget)
        _ = _authService.UpdateLastUsedAsync(apiKey);

        var keyInfo = await _authService.GetApiKeyInfoAsync(apiKey);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, keyInfo?.User?.Username ?? "api-user"),
            new Claim("ApiKeyId", keyInfo?.Id.ToString() ?? "0"),
        };

        if (keyInfo?.User?.IsAdmin == true)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
