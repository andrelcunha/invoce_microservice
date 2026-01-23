namespace InvoiceMicroservice.Infrastructure.Services;

/// <summary>
/// Configuration options for IPM API client.
/// </summary>
public record ApiClientOptions
{
    public int TimeoutSeconds { get; init; } = 30;
    public int RetryAttempts { get; init; } = 3;
}