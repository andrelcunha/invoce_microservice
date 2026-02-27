namespace InvoiceMicroservice.Domain.Entities;

public class InvoiceEmissionResult
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // 1:1 with queue job
    public Guid JobId { get; set; }
    public InvoiceEmissionJob Job { get; set; } = null!;

    public string IssuerCnpj { get; set; } = null!;
    public string PortalType { get; set; } = null!; // "Ipm" | "Nacional"

    public DateTime? IssuedAt { get; set; }
    public DateTime? ProviderProcessedAt { get; set; }

    public string? NumeroDfe { get; set; }
    public string? SerieDfe { get; set; }

    public string? CodStatus { get; set; }
    public string? StatusDescription { get; set; }

    public string? Protocolo { get; set; }
    public string? ChaveAcesso { get; set; }          // Nacional - NFSe retrival key
    public string? VerificationCode { get; set; }     // IPM
    public string? DocumentUrl { get; set; }          // IPM link_nfse

    public string? RequestXml { get; set; }           // logical XML sent
    public string? ResponseRaw { get; set; }          // exact provider response
    public string? AlertsJson { get; set; }           // Nacional alertas[]
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}