

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
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/v1.json", "v1");
                options.RoutePrefix = "swagger"; // Set Swagger UI at app's root
            });
        }

    }
}
