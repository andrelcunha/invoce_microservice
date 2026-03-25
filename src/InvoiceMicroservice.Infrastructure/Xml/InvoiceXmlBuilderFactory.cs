using System.Text.Json;
using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InvoiceMicroservice.Infrastructure.Xml;

/// <summary>
/// Factory that selects IPM vs Nacional XML builder based on portal credentials.
/// Single lookup by issuer CNPJ returns both municipality data and portal type.
/// </summary>
public class InvoiceXmlBuilderFactory : IInvoiceXmlBuilderFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IPortalCredentialsRepository _credentialsRepo;
    private readonly IIssuerRepository _issuerRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InvoiceXmlBuilderFactory> _logger;

    public InvoiceXmlBuilderFactory(
        IPortalCredentialsRepository credentialsRepo,
        IIssuerRepository issuerRepository,
        IServiceProvider serviceProvider,

        ILogger<InvoiceXmlBuilderFactory> logger)
    {
        _credentialsRepo = credentialsRepo;
        _issuerRepository = issuerRepository;
        _serviceProvider = serviceProvider;

        _logger = logger;
    }

    public async Task<IInvoiceXmlBuilder> GetBuilderAsync(
        string issuerCnpj,
        CancellationToken cancellationToken = default)
    {
        var cnpj = new Cnpj(issuerCnpj);
        var issuer = await _issuerRepository.GetByCnpjAsync(cnpj, cancellationToken);
        var credentials = issuer?.PortalCredentials;
        if (!string.IsNullOrWhiteSpace(issuer?.AddressJson))
        {
            Address? address = string.IsNullOrWhiteSpace(issuer.AddressJson)
                ? null
                : JsonSerializer.Deserialize<Address>(issuer.AddressJson, JsonOptions);

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
                "Selected {Portal} XML builder for CNPJ {Cnpj} (city: {City}/{Uf})",
                credentials.PortalType,
                issuerCnpj,
                address?.City,
                address?.Uf);
            return GetBuilder(credentials.PortalType);
        }
        else
        {
            _logger.LogWarning(
                "Issuer with CNPJ {Cnpj} has no address configured. Cannot determine city or state.",
                issuerCnpj);
            throw new InvalidOperationException(
                $"Issuer with CNPJ {issuerCnpj} has no address configured. " +
                $"Cannot determine portal type without city/state info. " +
                $"Add issuer address via admin API before issuing invoices.");
        }
    }

    /// <summary>
    /// Resolves and instantiates the appropriate XML builder based on portal type.
    /// </summary>
    private IInvoiceXmlBuilder GetBuilder(PortalType portalType)
    {
        return portalType switch
        {
            PortalType.IPM => _serviceProvider.GetRequiredService<IpmXmlBuilder>(),
            PortalType.Nacional => _serviceProvider.GetRequiredService<NationalXmlBuilder>(),
            // Future: PortalType.BETHA => _serviceProvider.GetRequiredService<BethaXmlBuilder>(),
            // Future: PortalType.GINFES => _serviceProvider.GetRequiredService<GinfesXmlBuilder>(),
            // Future: PortalType.WEBISS => _serviceProvider.GetRequiredService<WebIssXmlBuilder>(),
            _ => throw new InvalidOperationException(
                $"Unsupported NFS-e portal type: '{portalType}'. " +
                $"Supported types: IPM, Nacional. " +
                $"Check portal_credentials.portal_type in database.")
        };
    }
}