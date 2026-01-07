using System.Text.Json;
using FluentValidation;
using InvoiceMicroservice.Api.Extensions;
using InvoiceMicroservice.Application.Commands.EmitInvoice;
using InvoiceMicroservice.Domain.Entities;
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

        builder.Services.AddDependencyInjection();

        builder.Services.AddAuthorization();

        builder.Services.AddOpenApiConfiguration();

        builder.Services.AddFluentValidationConfiguration();

        var app = builder.Build();


        // Configure the HTTP request pipeline.
        app.UseOpenApiConfiguration();
        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapGet("/", () => Results.Ok("Invoice Microservice - Alive"));

        app.MapPost("/api/invoices", async (
            EmitInvoiceCommand command,
            IValidator<EmitInvoiceCommand> validator,
            IInvoiceRepository repository,
            HttpContext context,
            CancellationToken ct) =>
        {
            var validationResult = await validator.ValidateAsync(command, ct);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var issuerCnpj = new Domain.ValueObjects.Cnpj(command.Data.Issuer.Cnpj);
            var issuerJson = JsonSerializer.Serialize(command.Data.Issuer);
            var consumerJson = JsonSerializer.Serialize(command.Data.Consumer);

            var invoice = Invoice.Create(
                command.ClientId,
                issuerCnpj,
                issuerJson,
                consumerJson,
                command.Data.ServiceDescription,
                command.Data.Amount,
                command.Data.IssuedAt,
                command.Data.ServiceTypeKey // Pass through service type key
            );

            await repository.AddAsync(invoice, ct);

            var location = $"/api/invoices/{invoice.Id}";

            return Results.Accepted(location, new { invoice.Id, invoice.Status });
        })
        .WithName("CreateInvoice")
        .WithOpenApi();

        app.MapGet("/api/invoices/{id:guid}", async (
            Guid id,
            IInvoiceRepository repository,
            CancellationToken ct) =>
        {
            var invoice = await repository.GetByIdAsync(id, ct);
            if (invoice is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new
            {
                invoice.Id,
                invoice.Status,
                invoice.ExternalInvoiceId,
                invoice.XmlPayload,
                invoice.CreatedAt,
                invoice.IssuedAt,
                // invoice.ClientId,
                // invoice.IssuerCnpj,
                // IssuerData = JsonSerializer.Deserialize<object>(invoice.IssuerData),
                // ConsumerData = JsonSerializer.Deserialize<object>(invoice.ConsumerData),
                // invoice.Amount,
                // invoice.ServiceDescription,
                // invoice.XMLResponse,
                // invoice.ExternalResponse,
                // invoice.ErrorDetails,
                // invoice.RetryCount,
                // invoice.UpdatedAt,
            });
        })
        .WithName("GetInvoiceStatus")
        .WithOpenApi();

        app.Run();
    }
}
