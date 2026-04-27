using FluentValidation;
using InvoiceMicroservice.Application.Commands.EmitInvoice;
using InvoiceMicroservice.Application.Commands.Issuer;
using InvoiceMicroservice.Application.Commands.PortalCredentials;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Infrastructure.Repositories;
using InvoiceMicroservice.Infrastructure.Xml;
using InvoiceMicroservice.Api.Background;

namespace InvoiceMicroservice.Api.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddDependencies(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<IApiClientRepository, ApiClientRepository>();
        services.AddScoped<IServiceTypeTaxMappingRepository, ServiceTypeTaxMappingRepository>();
        services.AddScoped<IIssuerRepository, IssuerRepository>();
        services.AddScoped<IPortalCredentialsRepository, PortalCredentialsRepository>();
        services.AddScoped<IInvoiceEmissionResultRepository, InvoiceEmissionResultRepository>();

        // XML Builders (concrete implementations - one per portal type)
        services.AddScoped<IpmXmlBuilder>();
        services.AddScoped<NationalXmlBuilder>();

        // XML Builder Factory (selects implementation based on credentials portal_type)
        services.AddScoped<IInvoiceXmlBuilderFactory, InvoiceXmlBuilderFactory>();

        // Command handlers
        services.AddScoped<EmitInvoiceCommandHandler>();
        services.AddScoped<RegisterIssuerCommandHandler>();
        services.AddScoped<CreatePortalCredentialsCommandHandler>();

        // Validators
        services.AddValidatorsFromAssemblyContaining<EmitInvoiceCommandValidator>();
        services.AddValidatorsFromAssemblyContaining<RegisterIssuerCommandValidator>();

        // Queue repository
        services.AddScoped<IInvoiceEmissionJobRepository, InvoiceEmissionJobRepository>();

        // Background worker
        services.AddHostedService<InvoiceEmissionWorker>();


        return services;
    }
}
