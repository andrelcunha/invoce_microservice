using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Infrastructure.Services;

namespace InvoiceMicroservice.Api.Extensions;

public static class IpmClientConfig
{
    public const string SectionName = "IpmClient";
    
    public static IServiceCollection AddIpmClientConfiguration(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        var ipmMode = configuration["IpmClient:Mode"] ?? "File";
        
        if (ipmMode.Equals("File", StringComparison.OrdinalIgnoreCase))
        {
            // File-based dummy client for XML validation
            var outputDirectory = configuration["IpmClient:FileOutputDirectory"];
            services.AddScoped<IIpmClient>(sp => 
                new FileIpmClient(
                    sp.GetRequiredService<ILogger<FileIpmClient>>(),
                    outputDirectory));
        }
        else if (ipmMode.Equals("Api", StringComparison.OrdinalIgnoreCase))
        {
            // Real IPM API client
            var options = new IpmApiClientOptions
            {
                BaseUrl = configuration["IpmClient:ApiBaseUrl"] 
                    ?? throw new InvalidOperationException("IpmClient:ApiBaseUrl required when Mode=Api"),
                Username = configuration["IpmClient:Username"] 
                    ?? throw new InvalidOperationException("IpmClient:Username required when Mode=Api"),
                Password = configuration["IpmClient:Password"] 
                    ?? throw new InvalidOperationException("IpmClient:Password required when Mode=Api"),
                TimeoutSeconds = int.Parse(configuration["IpmClient:TimeoutSeconds"] ?? "30"),
                RetryAttempts = int.Parse(configuration["IpmClient:RetryAttempts"] ?? "3"),
                RequiresSignature = bool.Parse(configuration["IpmClient:RequiresSignature"] ?? "false"),
                CertificatePath = configuration["IpmClient:CertificatePath"],
                CertificatePassword = configuration["IpmClient:CertificatePassword"]
            };

            services.AddHttpClient<IIpmClient, IpmApiClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    UseCookies = false, // Manual cookie management via CookieContainer
                    AllowAutoRedirect = false // Per integration guide, avoid redirects
                });

            services.AddScoped<IIpmClient>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(nameof(IpmApiClient));
                var logger = sp.GetRequiredService<ILogger<IpmApiClient>>();
                
                return new IpmApiClient(httpClient, logger, options);
            });
        }
        else
        {
            throw new InvalidOperationException(
                $"IPM Client mode '{ipmMode}' not recognized. Use 'File' or 'Api'.");
        }

        return services;
    }
}