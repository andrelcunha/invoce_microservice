using InvoiceMicroservice.Domain.Entities;

namespace InvoiceMicroservice.Infrastructure.Configuration;

public class PortalConfig
{
    /// <summary>
    /// Homologação/produção-restrita base URL. Used as-is for portals without a
    /// separate legal-production environment (e.g. IPM), and as the fallback for
    /// Nacional when <see cref="ApiBaseUrlProducao"/> isn't set or isTestMode is true.
    /// </summary>
    public string ApiBaseUrl { get; set; } = null!;

    /// <summary>
    /// Real production base URL (legally valid documents). Only meaningful for
    /// portals with a genuinely separate production environment, such as
    /// SEFIN Nacional. Leave unset for portals like IPM that only have one host.
    /// </summary>
    public string? ApiBaseUrlProducao { get; set; }

    public PortalEndpoints Endpoints { get; set; } = null!;
    public int TimeoutSeconds { get; set; } = 30;
    public int RetryAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 5;
}

public class PortalEndpoints
{
    public string EmitInvoice { get; set; } = null!;
    public string QueryInvoice { get; set; } = null!;
    public string CancelInvoice { get; set; } = null!;
}

public class PortalConfigs
{
    public const string SectionName = "PortalConfigs";
    public PortalConfig Ipm { get; set;} = null!;
    public PortalConfig Nacional {get;set;} = null!;

    public PortalConfig GetConfig(PortalType portalType)
    {
        return portalType switch
        {
            PortalType.IPM => Ipm,
            PortalType.Nacional => Nacional,
            _ => throw new ArgumentException($"Unsupported portal type: {portalType}. Supported types: IPM, Nacional.", nameof(portalType))
        };
    }
}