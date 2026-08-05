namespace InvoiceMicroservice.Application.Commands.PortalCredentials;

/// <summary>
/// DTO for updating existing portal credentials.
/// </summary>
public record UpdatePortalCredentialsDto
{
    /// <summary>
    /// Portal API base URL (may differ per municipality).
    /// Example: https://concordia.atende.net/?pg=rest&service=WNERestServiceNFSe&cidade=padrao
    /// </summary>
    public required string ApiBaseUrl { get; init; }
    
    /// <summary>
    /// Portal username (Basic Auth).
    /// </summary>
    public required string Username { get; init; }
    
    /// <summary>
    /// Portal password (Basic Auth) - will be hashed.
    /// Leave null to keep existing password.
    /// </summary>
    public string? Password { get; init; }
    
    /// <summary>
    /// Whether digital signature is required for this municipality.
    /// </summary>
    public bool RequiresSignature { get; init; }
    
    /// <summary>
    /// Base64-encoded PFX certificate data.
    /// Leave null to keep existing certificate.
    /// </summary>
    public string? CertificateBase64 { get; init; }
    
    /// <summary>
    /// Certificate password.
    /// Leave null to keep existing certificate password.
    /// </summary>
    public string? CertificatePassword { get; init; }
}
