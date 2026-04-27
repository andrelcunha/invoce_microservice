using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using InvoiceMicroservice.Domain.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace InvoiceMicroservice.Api.Authentication;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IApiClientRepository _clients;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiClientRepository clients)
        : base(options, logger, encoder)
    {
        _clients = clients;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var headerValues))
            return AuthenticateResult.NoResult();

        if (headerValues.Count != 1)
            return AuthenticateResult.Fail("A single API key value is required.");

        var providedKey = headerValues[0];
        if (string.IsNullOrWhiteSpace(providedKey))
            return AuthenticateResult.Fail("Invalid API key.");

        var hash = ComputeHash(providedKey);
        var client = await _clients.GetByApiKeyHashAsync(hash);

        if (client is null)
            return AuthenticateResult.Fail("Invalid API key.");

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, client.ClientId),
            new Claim(ClaimTypes.NameIdentifier, client.ClientId)
        };

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationOptions.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationOptions.SchemeName);

        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.Append("WWW-Authenticate", ApiKeyAuthenticationOptions.SchemeName);
        return Task.CompletedTask;
    }

    internal static string ComputeHash(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
