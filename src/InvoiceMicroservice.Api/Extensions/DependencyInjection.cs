using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Infrastructure.Repositories;
using InvoiceMicroservice.Infrastructure.Xml;

namespace InvoiceMicroservice.Api.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddDependencyInjection(this IServiceCollection services)
    {
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IServiceTypeTaxMappingRepository, ServiceTypeTaxMappingRepository>();
        services.AddScoped<IMunicipalityRepository, MunicipalityRepository>();
        services.AddScoped<IIpmXmlBuilder, IpmXmlBuilder>();
        return services;
    }

}
