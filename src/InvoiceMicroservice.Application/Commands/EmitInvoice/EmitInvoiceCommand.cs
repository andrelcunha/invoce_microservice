using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
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
    public required int NfseSeries { get; init; }
    public required int NfseNumber { get; init; }
    public required Consumer Consumer { get; init; }
    public required string ServiceDescription { get; init; }
    public required decimal Amount { get; init; }
    public DateTime IssuedAt { get; init; }
    public string? ServiceTypeKey { get; init; }
    public string? MunicipalTaxCode { get; init; }
    public decimal IssRate { get; init; }
    public int? PisCofinsCts { get; init; }
    public decimal AliquotaPis { get; init; }
    public decimal AliquotaCofins { get; init; }
    public string TipoRetencaoPisCofins { get; init; } = null!;
    public string IbsCbsClassTrib { get; init; } = null!;
    public string IbsCbsCst { get; init; } = null!;
}

public record EmitInvoiceJobPayload
{
    public int SchemaVersion { get; init; } = 1;
    public DateTime EnqueuedAtUtc { get; init; } = DateTime.UtcNow;
    public required EmitInvoiceCommand Command { get; init; }
}

public class EmitInvoiceCommandHandler
{
    private readonly IIssuerRepository _issuerRepository;
    private readonly IInvoiceEmissionJobRepository _jobRepository;
    private readonly ILogger<EmitInvoiceCommandHandler> _logger;

    public EmitInvoiceCommandHandler(
            IIssuerRepository issuerRepository,
            IInvoiceEmissionJobRepository jobRepository,
            ILogger<EmitInvoiceCommandHandler> logger)
    {
        _issuerRepository = issuerRepository;
        _jobRepository = jobRepository;
        _logger = logger;
    }

    public async Task<Guid> HandleAsync(EmitInvoiceCommand request, CancellationToken cancellationToken = default)
    {
        var issuerCnpj = new Cnpj(request.IssuerCnpj);

        // Keep lightweight preconditions in API path
        var issuer = await _issuerRepository.GetByCnpjAsync(issuerCnpj, cancellationToken)
            ?? throw new InvalidOperationException($"Issuer with CNPJ {issuerCnpj.Value} not found.");

        if (!issuer.IsActive)
            throw new InvalidOperationException($"Issuer with CNPJ {issuerCnpj.Value} is inactive.");

        var payload = new EmitInvoiceJobPayload
        {
            Command = request
        };

        var payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var job = new InvoiceEmissionJob
        {
            Id = Guid.NewGuid(),
            IssuerCnpj = issuerCnpj.Value,
            PayloadJson = payloadJson,
            Status = InvoiceEmissionJobStatus.Pending,
            Attempts = 0,
            NextRetryAt = null,
            LastError = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _jobRepository.AddAsync(job, cancellationToken);

        _logger.LogInformation(
            "Invoice emission job enqueued. JobId={JobId}, IssuerCnpj={IssuerCnpj}, ClientId={ClientId}",
            job.Id, issuerCnpj.Value, request.ClientId);

        return job.Id;
    }
}