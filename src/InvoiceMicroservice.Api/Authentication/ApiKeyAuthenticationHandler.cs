using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace InvoiceMicroservice.Api.Authentication;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (headerValues.Count != 1)
        {
            return Task.FromResult(AuthenticateResult.Fail("A single API key value is required."));
        }

        if (string.IsNullOrWhiteSpace(Options.Key))
        {
            return Task.FromResult(AuthenticateResult.Fail("API key authentication is not configured."));
        }

        var providedKey = headerValues[0];
        if (string.IsNullOrWhiteSpace(providedKey) || !KeysMatch(providedKey, Options.Key))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "PassouLavouAPI"),
            new Claim(ClaimTypes.NameIdentifier, "passou-lavou-api")
        };

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationOptions.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationOptions.SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.Append("WWW-Authenticate", ApiKeyAuthenticationOptions.SchemeName);
        return Task.CompletedTask;
    }

    private static bool KeysMatch(string providedKey, string configuredKey)
    {
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);
        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);

        return providedBytes.Length == configuredBytes.Length &&
               CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
    }
}
