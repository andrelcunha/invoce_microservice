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
        services.Configure<PortalConfigs>(configuration.GetSection(PortalConfigs.SectionName));
        services.AddScoped<IpmApiClient>();
        services.AddScoped<NationalApiClient>();
        return services;
    }
}