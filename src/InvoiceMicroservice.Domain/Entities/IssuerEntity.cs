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

    public PortalCredentials PortalCredentials { get; set; } = null!;
}

