using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Domain.ValueObjects;
using InvoiceMicroservice.Infrastructure.Xml;
using System.Text.Json;

namespace InvoiceMicroservice.Application.Commands.EmitInvoice;

public record EmitInvoiceCommand
{
    public required string ClientId { get; init; }
    public required EmitInvoiceData Data { get; init; }
    public bool IsTestMode { get; init; } = true; // Default to test mode for safety
}

public record EmitInvoiceData
{
    public required Issuer Issuer { get; init; }
    public required Consumer Consumer { get; init; }
    public required string ServiceDescription { get; init; }
    public required decimal Amount { get; init; }
    public DateTime IssuedAt { get; init; }
    public string? ServiceTypeKey { get; init; }
}

public class EmitInvoiceCommandHandler
{
    private readonly IInvoiceRepository _repository;
    private readonly IIpmXmlBuilder _xmlBuilder;
    private readonly IIpmClient _ipmClient;

    public EmitInvoiceCommandHandler(
        IInvoiceRepository repository, 
        IIpmXmlBuilder xmlBuilder,
        IIpmClient ipmClient)
    {
        _repository = repository;
        _xmlBuilder = xmlBuilder;
        _ipmClient = ipmClient;
    }

    public async Task<Guid> HandleAsync(EmitInvoiceCommand request, CancellationToken cancellationToken = default)
    {
        var issuerCnpj = new Cnpj(request.Data.Issuer.Cnpj);
        
        var issuerJson = JsonSerializer.Serialize(request.Data.Issuer);
        var consumerJson = JsonSerializer.Serialize(request.Data.Consumer);

        var invoice = Invoice.Create(
            request.ClientId,
            issuerCnpj,
            issuerJson,
            consumerJson,
            request.Data.ServiceDescription,
            request.Data.Amount,
            request.Data.IssuedAt,
            request.Data.ServiceTypeKey
        );

        await _repository.AddAsync(invoice, cancellationToken);

        // Generate XML
        var xml = await _xmlBuilder.BuildInvoiceXmlAsync(
            invoice, 
            isTestMode: request.IsTestMode, 
            cancellationToken);
        
        // Store generated XML
        invoice.XmlPayload = xml;
        
        // Submit to IPM (File or API depending on configuration)
        var result = await _ipmClient.SubmitInvoiceAsync(
            xml, 
            isTestMode: request.IsTestMode, 
            cancellationToken);
        
        // Update invoice with submission result
        if (result.Success)
        {
            invoice.MarkAsEmitted(
                result.InvoiceNumber ?? "",
                result.Protocol ?? "",
                result.VerificationCode ?? "",
                result.RawResponse ?? xml
            );
        }
        else
        {
            invoice.MarkAsFailed(string.Join("; ", result.Messages));
        }
        
        await _repository.UpdateAsync(invoice, cancellationToken);

        return invoice.Id;
    }
}