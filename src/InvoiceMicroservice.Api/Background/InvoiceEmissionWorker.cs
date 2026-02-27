using InvoiceMicroservice.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using InvoiceMicroservice.Application.Commands.EmitInvoice;
using InvoiceMicroservice.Domain.Entities;
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
                var issuerRepository = scope.ServiceProvider.GetRequiredService<IIssuerRepository>();
                var invoiceRepository = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
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

                        var invoice = Invoice.Create(
                            request.ClientId,
                            issuerCnpj,
                            issuerJson,
                            request.Data.NfseSeries,
                            request.Data.NfseNumber,
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

                        await invoiceRepository.AddAsync(invoice, stoppingToken);

                        var xmlBuilder = await xmlBuilderFactory.GetBuilderAsync(issuerCnpj.Value, stoppingToken);
                        var xml = await xmlBuilder.BuildInvoiceXmlAsync(invoice, request.IsTestMode, stoppingToken);

                        invoice.XmlPayload = xml;

                        var apiClient = xmlBuilder.GetApiClient();
                        var result = await apiClient.SubmitInvoiceAsync(
                            xml,
                            issuerCnpj.Value,
                            request.IsTestMode,
                            stoppingToken);

                        if (result.Success)
                        {
                            invoice.MarkAsEmitted(
                                result.InvoiceNumber ?? string.Empty,
                                result.Protocol ?? string.Empty,
                                result.VerificationCode ?? string.Empty,
                                result.RawResponse ?? string.Empty
                            );
                        }
                        else
                        {
                            invoice.MarkAsFailed(string.Join("; ", result.Messages));
                        }

                        await invoiceRepository.UpdateAsync(invoice, stoppingToken);
                        await jobs.MarkSucceededAsync(job.Id, stoppingToken);

                        logger.LogInformation(
                            "Processed invoice emission job {JobId}. InvoiceId={InvoiceId}, Success={Success}",
                            job.Id,
                            invoice.Id,
                            result.Success);
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
}