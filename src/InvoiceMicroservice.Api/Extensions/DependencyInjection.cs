using InvoiceMicroservice.Application.Commands.EmitInvoice;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Infrastructure.Repositories;
using InvoiceMicroservice.Infrastructure.Xml;

namespace InvoiceMicroservice.Api.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddDependencyInjection(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IServiceTypeTaxMappingRepository, ServiceTypeTaxMappingRepository>();
        services.AddScoped<IMunicipalityRepository, MunicipalityRepository>();
        services.AddScoped<IPortalCredentialsRepository, PortalCredentialsRepository>();

        // Services
        services.AddScoped<IInvoiceXmlBuilder, IpmXmlBuilder>();

        // Command handlers
        services.AddScoped<EmitInvoiceCommandHandler>();


        return services;
    }

}
