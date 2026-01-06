using InvoiceMicroservice.Infrastructure.Xml;
using Microsoft.Extensions.Options;

namespace InvoiceMicroservice.Api.Extensions;

public static class TaxConfigExtensions
{
    public static IServiceCollection AddTaxConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TaxConfig>(configuration.GetSection("TaxConfig"));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<TaxConfig>>().Value);

        return services;
    }
}