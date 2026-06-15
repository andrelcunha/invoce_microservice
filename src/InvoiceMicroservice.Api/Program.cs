using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using InvoiceMicroservice.Api.Extensions;
using InvoiceMicroservice.Application.Commands.EmitInvoice;
using InvoiceMicroservice.Domain.Interfaces;

namespace InvoiceMicroservice.Api;

public class Program
{
    public static void Main(string[] args)
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

        // Configure the HTTP request pipeline.
        app.UseOpenApiConfiguration();
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}
