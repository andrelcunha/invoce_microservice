using System.Text.Json;
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
        builder.Services.AddIpmClientConfiguration(builder.Configuration);
        builder.Services.AddDependencyInjection();
        builder.Services.AddAuthorization();
        builder.Services.AddControllers();
        builder.Services.AddOpenApiConfiguration();
        builder.Services.AddFluentValidationConfiguration();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        app.UseOpenApiConfiguration();
        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}
