namespace InvoiceMicroservice.Application.Commands.PortalCredentials;

public record CreatePortalCredentialsCommand
{
    public required string IssuerCnpj { get; init; }
    
    public required string Username { get; init; }
    
    public required string Password { get; init; }

    public bool RequiresSignature { get; init; }
    
    public byte[]? Certificate { get; init; }
    public string? CertificatePassword { get; init; }

    public int MunicipalityId { get; init; }

    public string PortalType { get; init; } = string.Empty;
}

