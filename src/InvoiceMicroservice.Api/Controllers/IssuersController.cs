using InvoiceMicroservice.Application.Commands.RegisterIssuer;
using InvoiceMicroservice.Application.Commands.UpdateIssuerCredentials;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceMicroservice.Api.Controllers;

[ApiController]
[Route("api/issuers")]
public class IssuersController : ControllerBase
{
    private readonly IIssuerRepository _issuerRepository;

    public IssuersController(IIssuerRepository issuerRepository)
    {
        _issuerRepository = issuerRepository;
    }

    /// <summary>
    /// Register a new issuer with initial portal credentials.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterIssuer(
        [FromBody] RegisterIssuerCommand command,
        CancellationToken cancellationToken)
    {
        // Validation + handler creates IssuerEntity + PortalCredentials together
        var issuerId = await _handler.HandleAsync(command, cancellationToken);
        
        return CreatedAtAction(
            nameof(GetIssuer),
            new { cnpj = command.Cnpj },
            new { id = issuerId });
    }

    /// <summary>
    /// Get issuer details including active portal credentials.
    /// </summary>
    [HttpGet("{cnpj}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIssuer(
        string cnpj,
        CancellationToken cancellationToken)
    {
        var issuer = await _issuerRepository.GetByCnpjAsync(new Cnpj(cnpj), cancellationToken);
        
        if (issuer == null)
            return NotFound();
        
        return Ok(issuer);
    }

    /// <summary>
    /// Update issuer master data (name, address, CNAE).
    /// </summary>
    [HttpPut("{cnpj}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateIssuer(
        string cnpj,
        [FromBody] UpdateIssuerCommand command,
        CancellationToken cancellationToken)
    {
        var issuer = await _issuerRepository.GetByCnpjAsync(new Cnpj(cnpj), cancellationToken);
        
        if (issuer == null)
            return NotFound();
        
        // Update via handler
        await _handler.HandleAsync(issuer.Id, command, cancellationToken);
        
        return NoContent();
    }

    /// <summary>
    /// Add or update portal credentials for an issuer.
    /// Nested under issuer resource.
    /// </summary>
    [HttpPut("{cnpj}/credentials/{portalType}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCredentials(
        string cnpj,
        PortalType portalType,
        [FromBody] UpdatePortalCredentialsCommand command,
        CancellationToken cancellationToken)
    {
        var issuer = await _issuerRepository.GetByCnpjAsync(new Cnpj(cnpj), cancellationToken);
        
        if (issuer == null)
            return NotFound();
        
        // Handler updates or creates credentials for this issuer+portalType
        await _handler.HandleAsync(issuer.Id, portalType, command, cancellationToken);
        
        return NoContent();
    }

    /// <summary>
    /// Delete portal credentials for an issuer.
    /// </summary>
    [HttpDelete("{cnpj}/credentials/{portalType}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCredentials(
        string cnpj,
        PortalType portalType,
        CancellationToken cancellationToken)
    {
        var issuer = await _issuerRepository.GetByCnpjAsync(new Cnpj(cnpj), cancellationToken);
        
        if (issuer == null)
            return NotFound();
        
        var credential = issuer.Credentials
            .FirstOrDefault(pc => pc.PortalType == portalType && pc.IsActive);
        
        if (credential == null)
            return NotFound();
        
        credential.Deactivate();
        await _issuerRepository.UpdateAsync(issuer, cancellationToken);
        
        return NoContent();
    }
}