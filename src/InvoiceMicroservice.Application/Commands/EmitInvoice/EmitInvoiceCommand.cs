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
}

public record EmitInvoiceData
{
    public required Issuer Issuer { get; init; }
    public required Consumer Consumer { get; init; }
    public required string ServiceDescription { get; init; }
    public required decimal Amount { get; init; }
    public required DateTime IssuedAt { get; init; }
    
    /// <summary>
    /// Optional service type key to lookup tax codes (e.g., "vehicle-wash-45200-05").
    /// If omitted, will attempt to infer from Issuer.Cnae or use default codes.
    /// </summary>
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

        // Generate XML (using test mode for now)
        var xml = await _xmlBuilder.BuildInvoiceXmlAsync(invoice, isTestMode: true, cancellationToken);
        
        // Submit to IPM (currently using FileIpmClient)
        var result = await _ipmClient.SubmitInvoiceAsync(xml, isTestMode: true, cancellationToken);
        
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