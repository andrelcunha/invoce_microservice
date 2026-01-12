namespace InvoiceMicroservice.Domain.Entities;

/// <summary>
/// Portal credentials for NFS-e emission tied to a specific municipality.
/// Each issuer (CNPJ) has credentials for their operating municipality's portal.
/// </summary>
public class PortalCredentials
{
    public Guid Id { get; private set; }
    
    /// <summary>
    /// Issuer CNPJ (unique identifier for the company).
    /// </summary>
    public string IssuerCnpj { get; private set; } = string.Empty;
    
    /// <summary>
    /// Reference to the municipality where this issuer operates.
    /// Determines TOM code and geographic data for XML generation.
    /// </summary>
    public int MunicipalityId { get; private set; }
    public Municipality Municipality { get; private set; } = null!; // Navigation property
    
    /// <summary>
    /// NFS-e portal type for this credential.
    /// Values: "IPM", "Nacional", "Betha", "GINFES", "WebISS", etc.
    /// Determines which XML builder and submission client to use.
    /// </summary>
    public string PortalType { get; private set; } = string.Empty;
    
    /// <summary>
    /// Portal API base URL (may differ per municipality even with same portal type).
    /// Example IPM: https://concordia.atende.net/?pg=rest&service=WNERestServiceNFSe&cidade=padrao
    /// Example Nacional: https://nfse.portoalegre.rs.gov.br/ws/nfse.asmx
    /// </summary>
    public string ApiBaseUrl { get; private set; } = string.Empty;
    
    /// <summary>
    /// Portal username (Basic Auth or similar).
    /// </summary>
    public string Username { get; private set; } = string.Empty;
    
    /// <summary>
    /// Portal password (encrypted at rest).
    /// </summary>
    public string PasswordHash { get; private set; } = string.Empty;
    
    /// <summary>
    /// Whether digital signature is required for this portal/municipality.
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
        int municipalityId,
        string portalType,
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
            MunicipalityId = municipalityId,
            PortalType = portalType.ToUpperInvariant(),
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