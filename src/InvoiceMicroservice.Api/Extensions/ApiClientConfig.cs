using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Infrastructure.Services;

namespace InvoiceMicroservice.Api.Extensions;

public static class ApiClientConfig
{
    public const string SectionName = "IpmClient";
    
    public static IServiceCollection AddApiClientConfiguration(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        var ipmMode = configuration["IpmClient:Mode"] ?? "File";
        
        if (ipmMode.Equals("File", StringComparison.OrdinalIgnoreCase))
        {
            // File-based dummy client for XML validation
            var outputDirectory = configuration["IpmClient:FileOutputDirectory"];
            services.AddScoped<IApiClient>(sp => 
                new FileInvoiceClient(
                    sp.GetRequiredService<ILogger<FileInvoiceClient>>(),
                    outputDirectory));
            services.AddScoped<IApiClient>(sp => 
                new FileInvoiceClient(
                    sp.GetRequiredService<ILogger<FileInvoiceClient>>(),
                    outputDirectory));
        }
        else if (ipmMode.Equals("Api", StringComparison.OrdinalIgnoreCase))
        {
            // Real IPM API client
            var options = new ApiClientOptions
            {
                // BaseUrl = configuration["IpmClient:ApiBaseUrl"] 
                //     ?? throw new InvalidOperationException("IpmClient:ApiBaseUrl required when Mode=Api"),
                // Username = configuration["IpmClient:Username"] 
                //     ?? throw new InvalidOperationException("IpmClient:Username required when Mode=Api"),
                // Password = configuration["IpmClient:Password"] 
                //     ?? throw new InvalidOperationException("IpmClient:Password required when Mode=Api"),
                TimeoutSeconds = int.Parse(configuration["IpmClient:TimeoutSeconds"] ?? "30"),
                RetryAttempts = int.Parse(configuration["IpmClient:RetryAttempts"] ?? "3"),
                // RequiresSignature = bool.Parse(configuration["IpmClient:RequiresSignature"] ?? "false"),
                // CertificatePath = configuration["IpmClient:CertificatePath"],
                // CertificatePassword = configuration["IpmClient:CertificatePassword"]
            };

            InitializeIpmApiClient(services, options);
            InitializeNationalApiClient(services, options);
        }
        else
        {
            throw new InvalidOperationException(
                $"IPM Client mode '{ipmMode}' not recognized. Use 'File' or 'Api'.");
        }

        return services;
    }

    private static void InitializeIpmApiClient(IServiceCollection services, ApiClientOptions options)
    {
        services.AddHttpClient<IApiClient, IpmApiClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseCookies = false, // Manual cookie management via CookieContainer
                AllowAutoRedirect = false // Per integration guide, avoid redirects
            });

        services.AddScoped<IApiClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(IpmApiClient));
            var logger = sp.GetRequiredService<ILogger<IpmApiClient>>();
            var credentialsRepo = sp.GetRequiredService<IPortalCredentialsRepository>();

            return new IpmApiClient(httpClient, logger, options, credentialsRepo);
        });
    }

    private static void InitializeNationalApiClient(IServiceCollection services, ApiClientOptions options)
    {
        services.AddHttpClient<IApiClient, NationalApiClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseCookies = false, // Manual cookie management via CookieContainer
                AllowAutoRedirect = false // Per integration guide, avoid redirects
            });

        services.AddScoped<IApiClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(NationalApiClient));
            var logger = sp.GetRequiredService<ILogger<NationalApiClient>>();
            var credentialsRepo = sp.GetRequiredService<IPortalCredentialsRepository>();

            return new NationalApiClient(httpClient, logger, options, credentialsRepo);
        });
    }
}