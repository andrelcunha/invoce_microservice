using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceMicroservice.Infrastructure.Repositories;

public class PortalCredentialsRepository : IPortalCredentialsRepository
{
    private readonly InvoiceDbContext _context;

    public PortalCredentialsRepository(InvoiceDbContext context)
    {
        _context = context;
    }

    public async Task<PortalCredentials?> GetByIssuerCnpjAsync(string issuerCnpj, CancellationToken cancellationToken = default)
    {
        return await _context.PortalCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IssuerCnpj == issuerCnpj && c.IsActive, cancellationToken);
    }

    public async Task AddAsync(PortalCredentials credentials, CancellationToken cancellationToken = default)
    {
        await _context.PortalCredentials.AddAsync(credentials, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PortalCredentials credentials, CancellationToken cancellationToken = default)
    {
        _context.PortalCredentials.Update(credentials);
        await _context.SaveChangesAsync(cancellationToken);
    }
}