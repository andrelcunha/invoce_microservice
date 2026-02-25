using InvoiceMicroservice.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceMicroservice.Api.Background;

public class InvoiceEmissionWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<InvoiceEmissionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerId = Environment.MachineName;

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var jobs = scope.ServiceProvider.GetRequiredService<IInvoiceEmissionJobRepository>();

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
                    // TODO: deserialize PayloadJson and call current emission flow
                    await jobs.MarkSucceededAsync(job.Id, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Job {JobId} failed", job.Id);
                    var retryAt = DateTime.UtcNow.AddSeconds(30);
                    await jobs.MarkFailedAsync(job.Id, ex.Message, permanent: false, retryAt, stoppingToken);
                }
            }
        }
    }
}