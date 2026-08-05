namespace InvoiceMicroservice.Domain.Entities;

public class InvoiceXmlPayload
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string ClientId { get; set; } = null!;
    public string IssuerCnpj { get; set; } = null!;

    public string IssuerData { get; set; } = null!;
    public int Series { get; set; }
    public int Number { get; set; }
    public string ConsumerData { get; set; } = null!;

    public decimal Amount { get; set; }
    public string ServiceDescription { get; set; } = null!;

    public DateTime? IssuedAt { get; set; }

    public string? ServiceTypeKey { get; set; }
    public string? MunicipalTaxCode { get; set; }

    public string? PisCofinsCts { get; set; }
    public decimal AliquotaPis { get; set; }
    public decimal AliquotaCofins { get; set; }
    public string? TipoRetencaoPisCofins { get; set; }

    public decimal IssRate { get; set; }

    public string? IbsCbsClassTrib { get; set; }
    public string? IbsCbsCst { get; set; }
}
