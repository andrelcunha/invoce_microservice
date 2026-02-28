using InvoiceMicroservice.Domain.Entities;

namespace InvoiceMicroservice.Domain.Interfaces;

public interface IInvoiceEmissionResultRepository
{
    Task UpsertByJobIdAsync(InvoiceEmissionResult result, CancellationToken ct = default);
    Task<InvoiceEmissionResult?> GetByJobIdAsync(Guid jobId, CancellationToken ct = default);
}