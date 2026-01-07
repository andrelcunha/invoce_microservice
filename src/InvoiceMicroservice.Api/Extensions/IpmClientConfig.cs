using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Infrastructure.Services;

namespace InvoiceMicroservice.Api.Extensions;

public static class IpmClientConfig
{
    public const string SectionName = "IpmClient";
    public static IServiceCollection AddIpmClientConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var ipmMode = configuration["IpmClient:Mode"] ?? "File";
        if (ipmMode.Equals("File", StringComparison.OrdinalIgnoreCase))
        {
            var outputDirectory = configuration["IpmClient:FileOutputDirectory"];
            services.AddScoped<IIpmClient>(sp => 
                new FileIpmClient(
                    sp.GetRequiredService<ILogger<FileIpmClient>>(),
                    outputDirectory));
        }
        else
        {
             // Future: Register real IpmClient implementation
            // services.AddScoped<IIpmClient, IpmApiClient>();
            throw new InvalidOperationException(
                $"IPM Client mode '{ipmMode}' not implemented yet. Use 'File' for now.");
        }
        

        return services;
    }
}