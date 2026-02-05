using InvoiceMicroservice.Domain.Enums;

namespace InvoiceMicroservice.Domain.Entities;

public class Issuer
{
    public string Cnpj { get; set; } = null!;
    public string MunicipalInscription { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Cnae { get; set; } = null!;
    public Address Address { get; set; } = null!;
    public RegimeTributario RegimeTributario { get; set; }
    public SubRegimeTributario SubRegimeTributario { get; set;}
}

public class Address
{
    public string Street { get; set; } = null!;
    public string Number { get; set; } = null!;
    public string? Complement { get; set; }
    public string Neighborhood { get; set; } = null!;
    public string City { get; set; } = null!;
    public string Uf { get; set; } = null!;
    public string ZipCode { get; set; } = null!;
    
    /// <summary>
    /// IBGE 7-digit municipal code (e.g., "4205407" for Florianópolis-SC).
    /// Used for tax jurisdiction and IPM API calls.
    /// </summary>
    public string IbgeCode { get; set; } = null!;
    
    /// <summary>
    /// TOM code for IPM API (e.g., "8083" for Concordia-SC).
    /// Falls back to IbgeCode if not specified.
    /// </summary>
    public string? TomCode { get; set; }
}

public class Consumer
{
    public string Name { get; set; } = null!;
    public string CpfCnpj { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public Address Address { get; set; } = null!;
}

public enum RegimeTributario
{
    SimplesNacional = 1,
    LucroPresumido = 2,
    LucroReal = 3, 
    Outro = 4
}

public enum SubRegimeTributario
{
    Nenhum,
    ME,
    EPP,
    MEI,
}


