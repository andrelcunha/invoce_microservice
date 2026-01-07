namespace InvoiceMicroservice.Domain.Entities;

public class Issuer
{
    public string Cnpj { get; set; } = null!;
    public string MunicipalInscription { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Cnae { get; set; } = null!;
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

public class Consumer
{
    public string Name { get; set; } = null!;
    public string CpfCnpj { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public Address Address { get; set; } = null!;
}


