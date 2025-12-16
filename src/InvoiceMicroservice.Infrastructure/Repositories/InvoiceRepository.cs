using System;
using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceMicroservice.Infrastructure.Repositories;

public class InvoiceRepository : IInvoiceRepository
{
    private readonly InvoiceDbContext _context;

    public InvoiceRepository(InvoiceDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Invoice invoice, CancellationToken ct = default)
    {
        await _context.Invoices.AddAsync(invoice, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public Task UpdateAsync(Invoice invoice, CancellationToken ct = default)
    {
        _context.Invoices.Update(invoice);
        return _context.SaveChangesAsync(ct);
    }
}
