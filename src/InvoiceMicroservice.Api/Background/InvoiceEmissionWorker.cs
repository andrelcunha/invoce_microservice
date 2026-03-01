using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Application.Commands.EmitInvoice;
using InvoiceMicroservice.Domain.ValueObjects;
using System.Text.Json;

namespace InvoiceMicroservice.Api.Background;

public class InvoiceEmissionWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<InvoiceEmissionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerId = Environment.MachineName;
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var jobs = scope.ServiceProvider.GetRequiredService<IInvoiceEmissionJobRepository>();
                var results = scope.ServiceProvider.GetRequiredService<IInvoiceEmissionResultRepository>();
                var issuerRepository = scope.ServiceProvider.GetRequiredService<IIssuerRepository>();
                var xmlBuilderFactory = scope.ServiceProvider.GetRequiredService<IInvoiceXmlBuilderFactory>();

                var batch = await jobs.ClaimPendingAsync(batchSize: 10, workerId, stoppingToken);

                if (batch.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                foreach (var job in batch)
                {
                    try
                    {
                        var payload = JsonSerializer.Deserialize<EmitInvoiceJobPayload>(job.PayloadJson, jsonOptions)
                            ?? throw new InvalidOperationException($"Invalid payload JSON for job {job.Id}.");

                        var request = payload.Command;
                        var issuerCnpj = new Cnpj(request.IssuerCnpj);
                        if (string.IsNullOrWhiteSpace(issuerCnpj.Value))
                            throw new InvalidOperationException($"Invalid issuer CNPJ in job {job.Id}.");

                        var issuer = await issuerRepository.GetByCnpjAsync(issuerCnpj, stoppingToken)
                            ?? throw new InvalidOperationException($"Issuer with CNPJ {issuerCnpj.Value} not found.");

                        if (!issuer.IsActive)
                            throw new InvalidOperationException($"Issuer with CNPJ {issuerCnpj.Value} is inactive.");

                        var issuerAddress = JsonSerializer.Deserialize<Address>(issuer.AddressJson, jsonOptions)
                            ?? throw new InvalidOperationException($"Invalid address data for issuer with CNPJ {issuerCnpj.Value}.");

                        var issuerDto = new IssuerDto
                        {
                            Cnpj = issuer.Cnpj.Value,
                            MunicipalInscription = issuer.MunicipalInscription,
                            Name = issuer.TradeName,
                            Cnae = issuer.Cnae,
                            Address = issuerAddress,
                            RegimeTributario = issuer.RegimeTributario,
                            SubRegimeTributario = issuer.SubRegimeTributario
                        };

                        var issuerJson = JsonSerializer.Serialize(issuerDto, jsonOptions);
                        var consumerJson = JsonSerializer.Serialize(request.Data.Consumer, jsonOptions);

                        var ctsPisCofins = request.Data.PisCofinsCts.HasValue
                            ? request.Data.PisCofinsCts.Value.ToString("D2")
                            : "00";

                        var invoice = new InvoiceXmlPayload
                        {
                            ClientId = request.ClientId,
                            IssuerCnpj = issuerCnpj.Value,
                            IssuerData = issuerJson,
                            Series = request.Data.NfseSeries,
                            Number = request.Data.NfseNumber,
                            ConsumerData = consumerJson,
                            ServiceDescription = request.Data.ServiceDescription,
                            Amount = request.Data.Amount,
                            IssuedAt = request.Data.IssuedAt,
                            IssRate = request.Data.IssRate,
                            MunicipalTaxCode = request.Data.MunicipalTaxCode,
                            PisCofinsCts = ctsPisCofins,
                            ServiceTypeKey = request.Data.ServiceTypeKey,
                            AliquotaPis = request.Data.AliquotaPis,
                            AliquotaCofins = request.Data.AliquotaCofins,
                            TipoRetencaoPisCofins = request.Data.TipoRetencaoPisCofins,
                            IbsCbsClassTrib = request.Data.IbsCbsClassTrib,
                            IbsCbsCst = request.Data.IbsCbsCst
                        };

                        var xmlBuilder = await xmlBuilderFactory.GetBuilderAsync(issuerCnpj.Value, stoppingToken);
                        var xml = await xmlBuilder.BuildInvoiceXmlAsync(invoice, request.IsTestMode, stoppingToken);

                        var portalType = ResolvePortalType(xmlBuilder);
                        var apiClient = xmlBuilder.GetApiClient();
                        var submit = await apiClient.SubmitInvoiceAsync(xml, issuerCnpj.Value, request.IsTestMode, stoppingToken);

                        await results.UpsertByJobIdAsync(new InvoiceEmissionResult
                        {
                            JobId = job.Id,
                            IssuerCnpj = issuerCnpj.Value,
                            PortalType = portalType,
                            IssuedAt = request.Data.IssuedAt,
                            NumeroDfe = submit.InvoiceNumber,
                            SerieDfe = request.Data.NfseSeries.ToString(),
                            CodStatus = submit.Success ? "SUCCESS" : "FAILED",
                            StatusDescription = string.Join("; ", submit.Messages ?? []),
                            Protocolo = submit.Protocol,
                            VerificationCode = submit.VerificationCode,
                            RequestXml = xml,
                            ResponseRaw = submit.RawResponse,
                            ChaveAcesso = submit.ChaveAcesso,
                            ErrorMessage = submit.Success ? null : string.Join("; ", submit.Messages ?? []),
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        }, stoppingToken);
                        await jobs.MarkSucceededAsync(job.Id, stoppingToken);

                        logger.LogInformation(
                            "Processed invoice emission job {JobId}. Success={Success}",
                            job.Id,
                            submit.Success);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            "Job {JobId} failed. Error={ErrorMessage}",
                            job.Id,
                            ex.Message);

                        var isPermanent = job.Attempts >= job.MaxAttempts;
                        DateTime? retryAt = isPermanent
                            ? null
                            : DateTime.UtcNow.AddSeconds(Math.Pow(2, Math.Max(1, job.Attempts)) * 15);

                        await results.UpsertByJobIdAsync(new InvoiceEmissionResult
                        {
                            JobId = job.Id,
                            IssuerCnpj = job.IssuerCnpj,
                            PortalType = "Unknown",
                            CodStatus = "FAILED",
                            StatusDescription = ex.Message,
                            ErrorMessage = ex.ToString(),
                            UpdatedAt = DateTime.UtcNow
                        }, stoppingToken);

                        await jobs.MarkFailedAsync(job.Id, ex.Message, permanent: isPermanent, retryAt, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Invoice emission worker loop failed; retrying after delay.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private static string ResolvePortalType(object builder) =>
        builder.GetType().Name.Contains("National", StringComparison.OrdinalIgnoreCase)
            ? "Nacional"
            : "Ipm";
}