using InvoiceMicroservice.Domain.Entities;

namespace InvoiceMicroservice.Domain.Interfaces;

public interface IInvoiceEmissionJobRepository
{
    Task AddAsync(InvoiceEmissionJob job, CancellationToken ct = default);
    Task<InvoiceEmissionJob?> GetByIdAsync(Guid jobId, CancellationToken ct = default);
    Task<IReadOnlyList<InvoiceEmissionJob>> ClaimPendingAsync(
        int batchSize,
        string workerId,
        CancellationToken ct = default);

    Task MarkSucceededAsync(Guid jobId, CancellationToken ct = default);
    Task MarkFailedAsync(Guid jobId, string error, bool permanent, DateTime? nextRetryAt, CancellationToken ct = default);
}