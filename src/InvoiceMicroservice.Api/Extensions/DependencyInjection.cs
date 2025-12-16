using System;
using InvoiceMicroservice.Infrastructure.Repositories;

namespace InvoiceMicroservice.Api.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddDependencyInjection(this IServiceCollection services)
    {
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();

        return services;
    }

}
