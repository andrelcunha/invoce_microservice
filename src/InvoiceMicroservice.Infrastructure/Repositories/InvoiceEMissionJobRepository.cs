using Microsoft.EntityFrameworkCore;
using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Infrastructure.Data;

namespace InvoiceMicroservice.Infrastructure.Repositories;

public class InvoiceEmissionJobRepository(InvoiceDbContext db) : IInvoiceEmissionJobRepository
{
    public async Task AddAsync(InvoiceEmissionJob job, CancellationToken ct = default)
    {
        db.InvoiceEmissionJobs.Add(job);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<InvoiceEmissionJob>> ClaimPendingAsync(int batchSize, string workerId, CancellationToken ct = default)
    {
        // PostgreSQL atomic claim using FOR UPDATE SKIP LOCKED + UPDATE ... RETURNING
        var now = DateTime.UtcNow;

        var claimed = await db.InvoiceEmissionJobs
            .FromSqlInterpolated($@"
                UPDATE ""invoice_emission_jobs"" j
                SET ""Status"" = 'Processing',
                    ""LockedAt"" = {now},
                    ""LockedBy"" = {workerId},
                    ""Attempts"" = j.""Attempts"" + 1,
                    ""UpdatedAt"" = {now}
                WHERE j.""Id"" IN (
                    SELECT ""Id""
                    FROM ""invoice_emission_jobs""
                    WHERE ""Status"" IN ('Pending','FailedRetryable')
                        AND (""NextRetryAt"" IS NULL OR ""NextRetryAt"" <= {now})
                    ORDER BY ""CreatedAt""
                    FOR UPDATE SKIP LOCKED
                    LIMIT {batchSize}
                )
                RETURNING *")
            .AsTracking()
            .ToListAsync(ct);

        return claimed;
    }

    public async Task MarkSucceededAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await db.InvoiceEmissionJobs.FirstAsync(x => x.Id == jobId, ct);
        job.Status = InvoiceEmissionJobStatus.Succeeded;
        job.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(Guid jobId, string error, bool permanent, DateTime? nextRetryAt, CancellationToken ct = default)
    {
        var job = await db.InvoiceEmissionJobs.FirstAsync(x => x.Id == jobId, ct);
        job.Status = permanent ? InvoiceEmissionJobStatus.FailedPermanent : InvoiceEmissionJobStatus.FailedRetryable;
        job.LastError = error.Length > 4000 ? error[..4000] : error;
        job.NextRetryAt = nextRetryAt;
        job.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}