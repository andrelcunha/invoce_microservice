namespace InvoiceMicroservice.Application.Commands.PortalCredentials;

/// <summary>
/// DTO for creating new portal credentials.
/// </summary>
public record CreatePortalCredentialsDto
{
    /// <summary>
    /// Issuer CNPJ (unique identifier for the company).
    /// </summary>
    public required string IssuerCnpj { get; init; }
    
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
    /// </summary>
    public required string Password { get; init; }
    
    /// <summary>
    /// Whether digital signature is required for this municipality.
    /// </summary>
    public bool RequiresSignature { get; init; }
    
    /// <summary>
    /// Certificate password.
    /// Required if RequiresSignature is true.
    /// </summary>
    public string? CertificatePassword { get; init; }

    public int MunicipalityId { get; init; }

    public string PortalType { get; init; } = string.Empty;
}
