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
            EmitInvoiceCommandHandler handler, // Inject the handler!
            CancellationToken ct) =>
        {
            var validationResult = await validator.ValidateAsync(command, ct);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            // Delegate to the handler - it handles persistence, XML generation, and IPM submission
            var invoiceId = await handler.HandleAsync(command, ct);

            var location = $"/api/invoices/{invoiceId}";
            return Results.Accepted(location, new { Id = invoiceId, Status = "Pending" });
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
                // invoice.ExternalProtocol,
                // invoice.VerificationCode,
                invoice.XmlPayload,
                // invoice.ResponsePayload,
                // invoice.ErrorMessage,
                invoice.RetryCount,
                invoice.CreatedAt,
                invoice.IssuedAt,
                invoice.UpdatedAt
            });
        })
        .WithName("GetInvoiceStatus")
        .WithOpenApi();

        app.Run();
    }
}
