using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Domain.ValueObjects;
using System.Text.Json;

namespace InvoiceMicroservice.Application.Commands.EmitInvoice;

public record EmitInvoiceCommand
{
    public required string ClientId { get; init; }
    public required string IssuerCnpj { get; init; }
    public required EmitInvoiceData Data { get; init; }
    public bool IsTestMode { get; init; } = true; // Default to test mode for safety
}

public record EmitInvoiceData
{
    public required Consumer Consumer { get; init; }
    public required string ServiceDescription { get; init; }
    public required decimal Amount { get; init; }
    public DateTime IssuedAt { get; init; }
    public string? ServiceTypeKey { get; init; }
    public string? MunicipalTaxCode { get; init; }
    
    /// <summary>
    /// ISS rate (Imposto Sobre Serviços) as percentage.
    /// Example: 5% = 0.05. Range: 2% to 5% depending on municipality and service.
    /// This is the CURRENT tax (separate from IBS/CBS which start in 2026).
    /// </summary>
    public decimal IssRate { get; init; }

    public int? PisCofinsCts { get; init; }

    public decimal AliquotaPis { get; init; }
    public decimal AliquotaCofins { get; init; }

    public string TipoRetencaoPisCofins { get; init; } = null!;

    public string IbsCbsClassTrib { get; init; } = null!;

    public string IbsCbsCst { get; init; } = null!;
}

public class EmitInvoiceCommandHandler
{
    private readonly IInvoiceRepository _repository;
    private readonly IInvoiceXmlBuilderFactory _xmlBuilderFactory;
    private readonly IApiClient _apiClient;
    private readonly IIssuerRepository _issuerRepository;

    public EmitInvoiceCommandHandler(
        IInvoiceRepository repository, 
        IInvoiceXmlBuilderFactory xmlBuilderFactory,
            IApiClient apiClient,
            IIssuerRepository issuerRepository)
    {
        _repository = repository;
        _xmlBuilderFactory = xmlBuilderFactory;
        _apiClient = apiClient;
        _issuerRepository = issuerRepository;
    }

    public async Task<Guid> HandleAsync(EmitInvoiceCommand request, CancellationToken cancellationToken = default)
    {
        var issuerCnpj = new Cnpj(request.IssuerCnpj);

        // fetch issuer from db
        var issuer = await _issuerRepository.GetByCnpjAsync(issuerCnpj, cancellationToken)
            ?? throw new InvalidOperationException($"Issuer with CNPJ {issuerCnpj.Value} not found.");

        if (!issuer.IsActive)
            throw new InvalidOperationException($"Issuer with CNPJ {issuerCnpj.Value} is inactive.");
        
        var issuerDto =  JsonSerializer.Deserialize<Issuer>(issuer.AddressJson);
        var issuerJson = JsonSerializer.Serialize(issuerDto);
        var consumerJson = JsonSerializer.Serialize(request.Data.Consumer);

        string ctsPisCofins = request.Data.PisCofinsCts.HasValue 
            ? request.Data.PisCofinsCts.Value.ToString("D2") 
            : "00";

        var invoice = Invoice.Create(
            request.ClientId,
            issuerCnpj,
            issuerJson,
            consumerJson,
            request.Data.ServiceDescription,
            request.Data.Amount,
            request.Data.IssuedAt,
            request.Data.IssRate,
            request.Data.MunicipalTaxCode,
            ctsPisCofins,
            request.Data.ServiceTypeKey,
            request.Data.AliquotaPis,
            request.Data.AliquotaCofins,
            request.Data.TipoRetencaoPisCofins,
            request.Data.IbsCbsClassTrib,
            request.Data.IbsCbsCst
        );

        await _repository.AddAsync(invoice, cancellationToken);

        // Factory selects IPM or Nacional builder based on issuer CNPJ
        var _xmlBuilder = await _xmlBuilderFactory.GetBuilderAsync(
            issuerCnpj.Value, 
            cancellationToken);

        // Generate XML
        var xml = await _xmlBuilder.BuildInvoiceXmlAsync(
            invoice, 
            isTestMode: request.IsTestMode, 
            cancellationToken);
        
        // Store generated XML
        invoice.XmlPayload = xml;
        
        // Submit to IPM (File or API depending on configuration)
        var result = await _apiClient.SubmitInvoiceAsync(
            xml,
            issuerCnpj.Value,
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