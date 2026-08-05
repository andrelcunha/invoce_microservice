using InvoiceMicroservice.Domain.Entities;

namespace InvoiceMicroservice.Application.Commands.Issuer;
public record UpdateIssuerCommand
{
    public required string Cnpj { get; init; }
    public required string MunicipalInscription { get; init; }
    public required string TradeName { get; init; }
    public required string LegalName { get; init; }
    public required string Cnae { get; init; }
    public required Address Address { get; init; }
    public required RegimeTributario RegimeTributario { get; init; }
    public required SubRegimeTributario SubRegimeTributario { get; init; }
    
    // Portal credentials (required at registration)
    public required PortalType PortalType { get; init; }
    public required string PortalUsername { get; init; }
    public required string PortalPassword { get; init; } // Will be hashed
    public bool RequiresSignature { get; init; }
    public byte[]? Certificate { get; init; }
    public string? CertificatePassword { get; init; }
}