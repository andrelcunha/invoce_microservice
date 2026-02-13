using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Infrastructure.Configuration;
using InvoiceMicroservice.Infrastructure.Services;
using Microsoft.Extensions.Options;

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
            InitializeIpmApiClient(services);
            InitializeNationalApiClient(services);
        }
        else
        {
            throw new InvalidOperationException(
                $"IPM Client mode '{ipmMode}' not recognized. Use 'File' or 'Api'.");
        }

        return services;
    }

    private static void InitializeIpmApiClient(IServiceCollection services)
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
            var options = sp.GetRequiredService<IOptions<PortalConfigs>>();

            return new IpmApiClient(httpClient, logger, options, credentialsRepo);
        });
    }

    private static void InitializeNationalApiClient(IServiceCollection services)
    {
        services.AddScoped<IApiClient>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<NationalApiClient>>();
            var credentialsRepo = sp.GetRequiredService<IPortalCredentialsRepository>();
            var options = sp.GetRequiredService<IOptions<PortalConfigs>>();
            return new NationalApiClient(logger, credentialsRepo, options);
        });
    }
}