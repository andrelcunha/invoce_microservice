using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using InvoiceMicroservice.Api.Models;
using InvoiceMicroservice.Application.Commands.PortalCredentials;
using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceMicroservice.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortalCredentialsController : ControllerBase
{
    private readonly IPortalCredentialsRepository _repository;
    private readonly IIssuerRepository _issuerRepository;
    private readonly IValidator<CreatePortalCredentialsCommand> _createValidator;
    private readonly IValidator<UpdatePortalCredentialsDto> _updateValidator;

    private readonly CreatePortalCredentialsCommandHandler _createHandler;

    public PortalCredentialsController(
        IPortalCredentialsRepository repository,
        IIssuerRepository issuerRepository,
        IValidator<CreatePortalCredentialsCommand> createValidator,
        IValidator<UpdatePortalCredentialsDto> updateValidator,
        CreatePortalCredentialsCommandHandler createHandler)
    {
        _repository = repository;
        _issuerRepository = issuerRepository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _createHandler = createHandler;
    }

    /// <summary>
    /// Gets all portal credentials (active only by default)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var credentials = await _repository.GetAllAsync(includeInactive, ct);
        
        var result = credentials.Select(c => new
        {
            c.Id,
            c.IssuerId,
            c.Username,
            c.RequiresSignature,
            HasCertificate = c.CertificateData != null,
            c.IsActive,
            c.CreatedAt,
            c.UpdatedAt
        });

        return Ok(result);
    }

    /// <summary>
    /// Gets a specific portal credential by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken ct = default)
    {
        var credentials = await _repository.GetByIdAsync(id, ct);
        if (credentials is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            credentials.Id,
            credentials.Issuer.Cnpj,
            credentials.Username,
            credentials.RequiresSignature,
            HasCertificate = credentials.CertificateData != null,
            credentials.IsActive,
            credentials.CreatedAt,
            credentials.UpdatedAt
        });
    }

    /// <summary>
    /// Gets portal credentials by issuer CNPJ
    /// </summary>
    [HttpGet("by-cnpj/{issuerCnpj}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIssuerCnpj(
        string issuerCnpj,
        CancellationToken ct = default)
    {
        var credentials = await _repository.GetByIssuerCnpjAsync(issuerCnpj, ct);
        if (credentials is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            credentials.Id,
            credentials.Issuer.Cnpj,
            credentials.Username,
            credentials.RequiresSignature,
            HasCertificate = credentials.CertificateData != null,
            credentials.IsActive,
            credentials.CreatedAt,
            credentials.UpdatedAt
        });
    }

    /// <summary>
    /// Creates new portal credentials
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(PortalCredentialsEntity), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromForm] CreatePortalCredentialsCommand dto, [FromForm] UploadCertificateForm? certificate = null)
    {
        if (dto.RequiresSignature)
        {
            if (certificate?.Certificate == null)
                return BadRequest(new { error = "Certificate is required when RequiresSignature is true" });

            if (!certificate.Certificate.FileName.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Certificate must be a .pfx file" });

            if (certificate.Certificate.Length > 5 * 1024 * 1024) // 5MB limit
                return BadRequest(new { error = "Certificate file must be less than 5MB" });
        }

        var validationResult = await _createValidator.ValidateAsync(dto);
        if (!validationResult.IsValid)
            return ValidationProblem(new ValidationProblemDetails(
                validationResult.ToDictionary()));

        byte[]? certificateData = await ExtractCertificateDataAsync(certificate);

        try
        {
            PortalCredentialsEntity? credentials = await _createHandler.HandleAsync(dto, certificateData, CancellationToken.None);
            if (credentials == null)
                return NotFound(new { error = $"Issuer with CNPJ {dto.IssuerCnpj} not found" });

            return CreatedAtAction(
                nameof(GetById),
                new { id = credentials.Id },
                new
                {
                    credentials.Id,
                    credentials.Issuer.Cnpj,
                    credentials.PortalType,
                    credentials.Username,
                    credentials.RequiresSignature,
                    HasCertificate = credentials.CertificateData != null,
                    credentials.IsActive,
                    credentials.CreatedAt
                });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    private static async Task<byte[]?> ExtractCertificateDataAsync(UploadCertificateForm? certificate)
    {
        // Read certificate bytes from uploaded file (only if provided)
        byte[]? certificateData = null;

        if (certificate?.Certificate != null && certificate.Certificate.Length > 0)
        {
            using var memoryStream = new MemoryStream();
            await certificate.Certificate.CopyToAsync(memoryStream);
            certificateData = memoryStream.ToArray();
        }

        return certificateData;
    }

    /// <summary>
    /// Updates existing portal credentials
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdatePortalCredentialsDto dto,
        CancellationToken ct = default)
    {
        var validationResult = await _updateValidator.ValidateAsync(dto, ct);
        if (!validationResult.IsValid)
        {
            var errors = new Dictionary<string, string[]>();
            foreach (var error in validationResult.Errors)
            {
                if (!errors.ContainsKey(error.PropertyName))
                    errors[error.PropertyName] = new[] { error.ErrorMessage };
                else
                    errors[error.PropertyName] = errors[error.PropertyName].Append(error.ErrorMessage).ToArray();
            }
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        var credentials = await _repository.GetByIdAsync(id, ct);
        if (credentials is null)
        {
            return NotFound();
        }

        // Hash password if provided, otherwise keep existing
        var passwordHash = !string.IsNullOrWhiteSpace(dto.Password)
            ? HashPassword(dto.Password)
            : credentials.PasswordHash;

        // Process certificate if provided, otherwise keep existing
        byte[]? certificateData = credentials.CertificateData;
        string? certificatePasswordHash = credentials.CertificatePasswordHash;

        if (!string.IsNullOrWhiteSpace(dto.CertificateBase64))
        {
            certificateData = Convert.FromBase64String(dto.CertificateBase64);
            certificatePasswordHash = !string.IsNullOrWhiteSpace(dto.CertificatePassword)
                ? HashPassword(dto.CertificatePassword)
                : certificatePasswordHash;
        }

        credentials.UpdateCredentials(
            dto.Username,
            passwordHash,
            dto.RequiresSignature,
            certificateData,
            certificatePasswordHash
        );

        await _repository.UpdateAsync(credentials, ct);

        return Ok(new
        {
            credentials.Id,
            credentials.Issuer.Cnpj,
            credentials.Username,
            credentials.RequiresSignature,
            HasCertificate = credentials.CertificateData != null,
            credentials.IsActive,
            credentials.UpdatedAt
        });
    }

    /// <summary>
    /// Soft deletes (deactivates) portal credentials
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken ct = default)
    {
        var credentials = await _repository.GetByIdAsync(id, ct);
        if (credentials is null)
        {
            return NotFound();
        }

        await _repository.DeleteAsync(id, ct);

        return NoContent();
    }

    private static string HashPassword(string password)
    {
        // // Using SHA256 for simplicity - in production, use BCrypt or Argon2
        // using var sha256 = SHA256.Create();
        // var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        // return Convert.ToBase64String(bytes);
        return password;
    }
}
