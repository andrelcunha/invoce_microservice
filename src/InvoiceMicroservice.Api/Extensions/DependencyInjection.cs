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
        services.AddScoped<IIssuerRepository, IssuerRepository>();

        // XML Builders (concrete implementations - one per portal type)
        services.AddScoped<IpmXmlBuilder>();
        services.AddScoped<NationalXmlBuilder>();

        // XML Builder Factory (selects implementation based on credentials portal_type)
        services.AddScoped<IInvoiceXmlBuilderFactory, InvoiceXmlBuilderFactory>();

        // Command handlers
        services.AddScoped<EmitInvoiceCommandHandler>();
        return services;
    }
}
