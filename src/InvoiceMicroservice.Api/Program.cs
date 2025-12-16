
using System.Text.Json;
using FluentValidation;
using InvoiceMicroservice.Api.Extensions;
using InvoiceMicroservice.Application.Commands.EmitInvoice;
using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Infrastructure.Repositories;

namespace InvoiceMicroservice.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddEFConfiguration(builder.Configuration);

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

            
            var invoice = new Invoice
            {
                ClientId = command.ClientId,
                IssuerCnpj = new Domain.ValueObjects.Cnpj(command.Data.Issuer.Cnpj),
                Amount = command.Data.Amount,
                ServiceDescription = command.Data.ServiceDescription,

                IssuerData = JsonSerializer.Serialize(command.Data.Issuer),
                ConsumerData = JsonSerializer.Serialize(command.Data.Consumer),

                Status = Domain.Enums.InvoiceStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            };

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
