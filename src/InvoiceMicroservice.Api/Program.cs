using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using InvoiceMicroservice.Api.Authentication;
using InvoiceMicroservice.Api.Extensions;
using InvoiceMicroservice.Application.Commands.EmitInvoice;
using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceMicroservice.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddEFConfiguration(builder.Configuration);
        builder.Services.AddTaxConfiguration(builder.Configuration);
        builder.Services.AddApiClientConfiguration(builder.Configuration);
        builder.Services.AddAuthenticationConfiguration(builder.Configuration);
        builder.Services.AddDependencies();
        builder.Services.AddControllers()
            .AddJsonOptions(opts =>
            {
                opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });
        builder.Services.AddOpenApiConfiguration();
        builder.Services.AddFluentValidationConfiguration();

        var app = builder.Build();

        // Seed the initial API client so the NestJS API can authenticate without a manual bootstrap step.
        // Idempotent: skips if the client already exists. Controlled by InitialClient:Id + InitialClient:ApiKey env vars.
        var initialClientId = app.Configuration["InitialClient:Id"];
        var initialApiKey   = app.Configuration["InitialClient:ApiKey"];
        if (!string.IsNullOrWhiteSpace(initialClientId) && !string.IsNullOrWhiteSpace(initialApiKey))
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InvoiceDbContext>();
            if (!await db.ApiClients.AnyAsync(c => c.ClientId == initialClientId))
            {
                db.ApiClients.Add(new ApiClient
                {
                    ClientId   = initialClientId,
                    ApiKeyHash = ApiKeyAuthenticationHandler.ComputeHash(initialApiKey),
                    IsActive   = true,
                });
                await db.SaveChangesAsync();
                app.Logger.LogInformation("Seeded initial API client: {ClientId}", initialClientId);
            }
        }

        // Configure the HTTP request pipeline.
        app.UseOpenApiConfiguration();
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}
