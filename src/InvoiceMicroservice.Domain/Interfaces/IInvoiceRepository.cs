using InvoiceMicroservice.Domain.Entities;

namespace InvoiceMicroservice.Domain.Interfaces;

public interface IInvoiceRepository
{
    Task AddAsync(Invoice invoice, CancellationToken ct = default);
    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(Invoice invoice, CancellationToken ct = default);
}
