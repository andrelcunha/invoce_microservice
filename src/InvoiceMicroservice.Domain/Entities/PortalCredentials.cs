namespace InvoiceMicroservice.Domain.Entities;

/// <summary>
/// Portal credentials for a specific issuer.
/// Each company needs its own login and optional certificate for digital signature.
/// </summary>
public class PortalCredentials
{
    public Guid Id { get; private set; }
    
    /// <summary>
    /// Issuer CNPJ (unique identifier for the company).
    /// </summary>
    public string IssuerCnpj { get; private set; } = string.Empty;
    
    /// <summary>
    /// Portal API base URL (may differ per municipality).
    /// Example: https://concordia.atende.net/?pg=rest&service=WNERestServiceNFSe&cidade=padrao
    /// </summary>
    public string ApiBaseUrl { get; private set; } = string.Empty;
    
    /// <summary>
    /// Portal username (Basic Auth).
    /// </summary>
    public string Username { get; private set; } = string.Empty;
    
    /// <summary>
    /// Portal password (Basic Auth) - ENCRYPTED in database.
    /// </summary>
    public string PasswordHash { get; private set; } = string.Empty;
    
    /// <summary>
    /// Whether digital signature is required for this municipality.
    /// </summary>
    public bool RequiresSignature { get; private set; }
    
    /// <summary>
    /// PFX certificate bytes (encrypted at rest).
    /// Null if signature not required.
    /// </summary>
    public byte[]? CertificateData { get; private set; }
    
    /// <summary>
    /// Certificate password (encrypted at rest).
    /// </summary>
    public string? CertificatePasswordHash { get; private set; }
    
    /// <summary>
    /// Whether these credentials are active.
    /// Allows soft-delete or temporary disable.
    /// </summary>
    public bool IsActive { get; private set; } = true;
    
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private PortalCredentials() { }

    public static PortalCredentials Create(
        string issuerCnpj,
        string apiBaseUrl,
        string username,
        string passwordHash,
        bool requiresSignature = false,
        byte[]? certificateData = null,
        string? certificatePasswordHash = null)
    {
        return new PortalCredentials
        {
            Id = Guid.NewGuid(),
            IssuerCnpj = issuerCnpj,
            ApiBaseUrl = apiBaseUrl,
            Username = username,
            PasswordHash = passwordHash,
            RequiresSignature = requiresSignature,
            CertificateData = certificateData,
            CertificatePasswordHash = certificatePasswordHash,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void UpdateCredentials(
        string apiBaseUrl,
        string username,
        string passwordHash,
        bool requiresSignature,
        byte[]? certificateData,
        string? certificatePasswordHash)
    {
        ApiBaseUrl = apiBaseUrl;
        Username = username;
        PasswordHash = passwordHash;
        RequiresSignature = requiresSignature;
        CertificateData = certificateData;
        CertificatePasswordHash = certificatePasswordHash;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}