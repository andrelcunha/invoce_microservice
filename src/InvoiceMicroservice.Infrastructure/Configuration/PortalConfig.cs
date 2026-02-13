using InvoiceMicroservice.Domain.Entities;

namespace InvoiceMicroservice.Infrastructure.Configuration;

public class PortalConfig
{
    public string ApiBaseUrl { get; set; } = null!;
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