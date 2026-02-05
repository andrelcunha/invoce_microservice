using InvoiceMicroservice.Domain.ValueObjects;

namespace InvoiceMicroservice.Domain.Entities;

public class IssuerEntity
{
    public Guid Id { get; set; }
    public Cnpj Cnpj { get; set; } = null!;
    public string MunicipalInscription { get; set; } = null!;
    public string TradeName { get; set; } = null!;
    public string LegalName { get; set; } = null!;
    public string Cnae { get; set; } = null!;
    public string AddressJson { get; set; } = null!;
    public RegimeTributario RegimeTributario { get; set; }
    public SubRegimeTributario SubRegimeTributario { get; set;}

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PortalCredentials> PortalCredentials { get; set; } = []; // should be one-to-one, because the issuer's municipality defines the portal credentials so there is no reason for an issuer to have more than one portal credentials
}

