namespace InvoiceMicroservice.Domain.Entities;

/// <summary>
/// Portal credentials for NFS-e emission tied to a specific municipality.
/// Each issuer (CNPJ) has credentials for their operating municipality's portal.
/// </summary>
public class PortalCredentials
{
    public Guid Id { get; private set; }
    public Guid IssuerId { get; private set; }
    public IssuerEntity Issuer { get; private set; } = null!; // Navigation property
    
    public PortalType PortalType { get; set; }

    // ApiBaseUrl shall be set via appsettings. 
    // We will create a configuration for each portal type to store the base URL and endpoints. 
    // public string ApiBaseUrl { get; private set; } = string.Empty; 

    public string Username { get; private set; } = string.Empty;
    
    public string PasswordHash { get; private set; } = string.Empty;
    
    public bool RequiresSignature { get; private set; }
    
    public byte[]? CertificateData { get; private set; }
    
    public string? CertificatePasswordHash { get; private set; }
    

    public bool IsActive { get; private set; } = true;
    
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public PortalCredentials() { }

    public static PortalCredentials Create(
        Guid issuerId,        
        PortalType portalType,
        string username,
        string passwordHash,
        bool requiresSignature = false,
        byte[]? certificateData = null,
        string? certificatePasswordHash = null)
    {
        return new PortalCredentials
        {
            Id = Guid.NewGuid(),
            IssuerId = issuerId,
            PortalType = portalType,
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
        string username,
        string passwordHash,
        bool requiresSignature,
        byte[]? certificateData,
        string? certificatePasswordHash)
    {
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

public enum PortalType
{
    IPM = 1,
    Nacional = 2
}