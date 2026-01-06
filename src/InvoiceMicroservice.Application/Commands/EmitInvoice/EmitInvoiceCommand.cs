namespace InvoiceMicroservice.Application.Commands.EmitInvoice;

public record EmitInvoiceCommand
{
    public required string ClientId { get; init; }
    public required EmitInvoiceData Data { get; init; }
}

public record EmitInvoiceData
{
    public required Issuer Issuer { get; init; }
    public required Consumer Consumer { get; init; }
    public required string ServiceDescription { get; init; }
    public required decimal Amount { get; init; }
    public required DateTime IssuedAt { get; init; }
    
    /// <summary>
    /// Optional service type key to lookup tax codes (e.g., "vehicle-wash-45200-05").
    /// If omitted, will attempt to infer from Issuer.Cnae or use default codes.
    /// </summary>
    public string? ServiceTypeKey { get; init; }
}

public class Issuer
{
    public string Cnpj { get; set; } = null!;
    public string MunicipalInscription { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Cnae { get; set; } = null!;
    public Address Address { get; set; } = null!;
}

public class Consumer
{
    public string Name { get; set; } = null!;
    public string CpfCnpj { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public Address Address { get; set; } = null!;
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
}