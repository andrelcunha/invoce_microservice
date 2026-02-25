using InvoiceMicroservice.Domain.Enums;
using InvoiceMicroservice.Domain.ValueObjects;

namespace InvoiceMicroservice.Domain.Entities;

public class Invoice
{
    public Guid Id { get; set; }
    public string ClientId { get; set; } = null!; // in this context, Client is the application that sent the invoice e.g. "accounting-api"

    public Cnpj IssuerCnpj { get; set; } = null!;

    // Store full objects as JSONB in PGSQL
    public string IssuerData { get; set; } = null!; // JSON 

    public required int Series { get; init; }
    public required int Number { get; init; }
    public string ConsumerData { get; set; } = null!; // JSON

    public decimal Amount { get; set; }
    public string ServiceDescription { get; set; } = null!;

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;

    public string? ExternalInvoiceId { get; set; } // ID returned by external tax authority system
    public string? XmlPayload { get; set; } // XML sent to tax authority
    public string? XMLResponse { get; set; } // XML received from tax authority
    public string? ExternalResponse { get; set; } // JSON parsed response

    public string? ErrorDetails { get; set; } // JSON error info

    public int RetryCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? IssuedAt { get; set; }

    public string? ServiceTypeKey { get; private set; }

    public string? PisCofinsCts { get; private set; }

    public decimal AliquotaPis { get; private set; }
    public decimal AliquotaCofins { get; private set; }
    public string? TipoRetencaoPisCofins { get; private set; } // 1 = Pis/Cofins Retido, 2 = Pis/Cofins Não Retido, 3 = Pis Retido Cofins Não Retido, 4 = Pis Não Retido Cofins Retido

    /// <summary>
    /// ISS rate (municipal service tax) as decimal (e.g., 0.05 = 5%).
    /// Stored separately from IBS/CBS rates which come from system config.
    /// </summary>
    public decimal IssRate { get; private set; }

    // Código de Tributação Municipal
    public string? MunicipalTaxCode { get; private set; }

    public string? IbsCbsClassTrib { get; private set; }
    public string? IbsCbsCst { get; private set; }

    private Invoice() { }

    public static Invoice Create(
        string clientId,
        Cnpj issuerCnpj,
        string issuerData,
        int series,
        int number,
        string consumerData,
        string serviceDescription,
        decimal amount,
        DateTime issuedAt,
        decimal issRate,
        string? municipalTaxCode = null,
        string? pisCofinsCts = null,
        string? serviceTypeKey = null,
        decimal aliquotaPis = 0,
        decimal aliquotaCofins = 0,
        string? tipoRetencaoPisCofins = null,
        string? ibsCbsClassTrib = null,
        string? ibsCbsCst = null
        )
    {
        return new Invoice
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            IssuerCnpj = issuerCnpj,
            IssuerData = issuerData,
            Series = series,
            Number = number,
            ConsumerData = consumerData,
            ServiceDescription = serviceDescription,
            Amount = amount,
            IssuedAt = issuedAt,
            ServiceTypeKey = serviceTypeKey,
            IssRate = issRate,
            Status = InvoiceStatus.Pending,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow,
            MunicipalTaxCode = municipalTaxCode,
            PisCofinsCts = pisCofinsCts,
            AliquotaPis = aliquotaPis,
            AliquotaCofins = aliquotaCofins,
            TipoRetencaoPisCofins = tipoRetencaoPisCofins,
            IbsCbsClassTrib = ibsCbsClassTrib,
            IbsCbsCst = ibsCbsCst
        };
    }

    public void MarkAsEmitted(string invoiceNumber, string protocol, string verificationCode, string responseXml)
    {
        Status = InvoiceStatus.Emitted;
        ExternalInvoiceId = invoiceNumber;
        // ExternalProtocol = protocol;
        // VerificationCode = verificationCode;
        // ResponsePayload = responseXml;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string errorMessage)
    {
        Status = InvoiceStatus.Failed;
        // ErrorMessage = errorMessage;
        RetryCount++;
        UpdatedAt = DateTime.UtcNow;
    }
}
