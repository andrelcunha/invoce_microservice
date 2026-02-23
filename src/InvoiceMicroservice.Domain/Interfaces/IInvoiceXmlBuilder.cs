using InvoiceMicroservice.Domain.Entities;

namespace InvoiceMicroservice.Domain.Interfaces;

/// <summary>
/// Factory that selects the appropriate XML builder based on issuer's municipality.
/// Different cities use different NFS-e portals (IPM, Nacional, Betha, etc.).
/// </summary>
public interface IInvoiceXmlBuilderFactory
{
    /// <summary>
    /// Gets the XML builder for the given issuer CNPJ.
    /// Looks up municipality and returns the appropriate implementation.
    /// </summary>
    Task<IInvoiceXmlBuilder> GetBuilderAsync(string issuerCnpj, CancellationToken cancellationToken = default);
}

/// <summary>
/// Base interface for all XML builders (IPM, Nacional, etc.).
/// </summary>
public interface IInvoiceXmlBuilder
{
    Task<string> BuildInvoiceXmlAsync(Invoice invoice, bool isTestMode = true, CancellationToken cancellationToken = default);

    PortalType GetPortalType();

    IApiClient GetApiClient();
}
