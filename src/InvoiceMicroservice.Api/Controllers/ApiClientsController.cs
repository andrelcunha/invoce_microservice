using System.Security.Cryptography;
using System.Text;
using InvoiceMicroservice.Api.Authentication;
using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InvoiceMicroservice.Api.Controllers;

[ApiController]
[Route("api/clients")]
[AllowAnonymous]
public class ApiClientsController : ControllerBase
{
    private readonly InvoiceDbContext _db;
    private readonly AdminKeyOptions _adminKey;

    public ApiClientsController(InvoiceDbContext db, IOptions<AdminKeyOptions> adminKey)
    {
        _db = db;
        _adminKey = adminKey.Value;
    }

    /// <summary>
    /// Registers a new API client. Requires X-Admin-Key header.
    /// Returns the plaintext API key and webhook secret once — store them securely, they are not retrievable again.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterClient(
        [FromBody] RegisterApiClientRequest request,
        CancellationToken ct)
    {
        if (!ValidateAdminKey())
            return Unauthorized();

        if (await _db.ApiClients.AnyAsync(c => c.ClientId == request.ClientId, ct))
            return BadRequest(new { error = $"Client '{request.ClientId}' already exists." });

        var rawApiKey = GenerateSecret("plv_");
        var rawWebhookSecret = GenerateSecret("whsec_");

        var client = new ApiClient
        {
            ClientId = request.ClientId,
            ApiKeyHash = ApiKeyAuthenticationHandler.ComputeHash(rawApiKey),
            WebhookUrl = request.WebhookUrl,
            WebhookSecret = rawWebhookSecret,
            IsActive = true
        };

        _db.ApiClients.Add(client);
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            client.ClientId,
            ApiKey = rawApiKey,
            WebhookSecret = rawWebhookSecret,
            client.WebhookUrl,
            note = "Store ApiKey and WebhookSecret securely. They will not be shown again."
        });
    }

    private bool ValidateAdminKey()
    {
        if (!Request.Headers.TryGetValue("X-Admin-Key", out var headerValues) || headerValues.Count != 1)
            return false;

        var provided = headerValues[0];
        if (string.IsNullOrWhiteSpace(provided) || string.IsNullOrWhiteSpace(_adminKey.Key))
            return false;

        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var configuredBytes = Encoding.UTF8.GetBytes(_adminKey.Key);

        return providedBytes.Length == configuredBytes.Length &&
                CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
    }

    private static string GenerateSecret(string prefix)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return prefix + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public record RegisterApiClientRequest(
    string ClientId,
    string? WebhookUrl);
