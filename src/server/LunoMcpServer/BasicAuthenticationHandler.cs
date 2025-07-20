using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace LunoMcpServer;

public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public BasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization Header"));

        string? authHeader = Request.Headers.Authorization;
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Basic ", System.StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization Header"));

        string token = authHeader.Substring("Basic ".Length).Trim();
        string credentialString;
        try
        {
            credentialString = Encoding.UTF8.GetString(Convert.FromBase64String(token));
        }
        catch
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Base64"));
        }
        var credentials = credentialString.Split(':', 2);
        if (credentials.Length != 2 || string.IsNullOrWhiteSpace(credentials[0]) || string.IsNullOrWhiteSpace(credentials[1]))
            return Task.FromResult(AuthenticateResult.Fail("Invalid Credentials Format"));

        // You can add credential validation here if needed
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, credentials[0]),
            new Claim("ApiKeyId", credentials[0]),
            new Claim("ApiKeySecret", credentials[1])
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
