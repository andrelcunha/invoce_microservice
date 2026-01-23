using InvoiceMicroservice.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InvoiceMicroservice.Infrastructure.Xml;

/// <summary>
/// Factory that selects IPM vs Nacional XML builder based on portal credentials.
/// Single lookup by issuer CNPJ returns both municipality data and portal type.
/// </summary>
public class InvoiceXmlBuilderFactory : IInvoiceXmlBuilderFactory
{
    private readonly IPortalCredentialsRepository _credentialsRepo;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InvoiceXmlBuilderFactory> _logger;

    public InvoiceXmlBuilderFactory(
        IPortalCredentialsRepository credentialsRepo,
        IServiceProvider serviceProvider,
        ILogger<InvoiceXmlBuilderFactory> logger)
    {
        _credentialsRepo = credentialsRepo;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<IInvoiceXmlBuilder> GetBuilderAsync(
        string issuerCnpj, 
        CancellationToken cancellationToken = default)
    {
        // Single lookup: credentials include municipality navigation property
        var credentials = await _credentialsRepo.GetByIssuerCnpjAsync(issuerCnpj, cancellationToken);
        
        if (credentials == null)
        {
            _logger.LogWarning(
                "No portal credentials found for CNPJ {Cnpj}. Cannot determine portal type.", 
                issuerCnpj);
            throw new InvalidOperationException(
                $"Portal credentials not configured for CNPJ {issuerCnpj}. " +
                $"Add credentials via admin API before issuing invoices.");
        }

        _logger.LogInformation(
            "Selected {PortalType} XML builder for CNPJ {Cnpj} (municipality: {Municipality}/{Uf})",
            credentials.PortalType,
            issuerCnpj,
            credentials.Municipality.Name,
            credentials.Municipality.Uf);
        return GetBuilder(credentials.PortalType);
    }

    /// <summary>
    /// Resolves and instantiates the appropriate XML builder based on portal type.
    /// </summary>
    private IInvoiceXmlBuilder GetBuilder(string portalType)
    {
        return portalType.ToUpperInvariant() switch
        {
            "IPM" => _serviceProvider.GetRequiredService<IpmXmlBuilder>(),
            "NACIONAL" => _serviceProvider.GetRequiredService<NationalXmlBuilder>(),
            // Future: "BETHA" => _serviceProvider.GetRequiredService<BethaXmlBuilder>(),
            // Future: "GINFES" => _serviceProvider.GetRequiredService<GinfesXmlBuilder>(),
            // Future: "WEBISS" => _serviceProvider.GetRequiredService<WebIssXmlBuilder>(),
            _ => throw new InvalidOperationException(
                $"Unsupported NFS-e portal type: '{portalType}'. " +
                $"Supported types: IPM, NACIONAL. " +
                $"Check portal_credentials.portal_type in database.")
        };
    }
}