using System;

namespace InvoiceMicroservice.Api.Extensions;

public static class OpenApiConfig
{
    public static void AddOpenApiConfiguration(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddOpenApi();
    }

    public static void UseOpenApiConfiguration(this WebApplication app)
    {
           // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

    }
}
