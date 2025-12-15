using InvoiceMicroservice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace InvoiceMicroservice.Api.Extensions;

public static class EFConfig
{
    public static IServiceCollection AddEFConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default") 
            ??  "Host=localhost;Port=5432;Database=invoice_db;Username=postgres;Password=root";
        services.AddDbContext<InvoiceDbContext>(options =>
            options.UseNpgsql(connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);
                }));

        return services;
    }
}
