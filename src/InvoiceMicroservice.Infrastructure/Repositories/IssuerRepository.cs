using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Domain.ValueObjects;
using InvoiceMicroservice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceMicroservice.Infrastructure.Repositories;

public class IssuerRepository : IIssuerRepository
{
    private readonly InvoiceDbContext _context;

    public IssuerRepository(InvoiceDbContext context)
    {
        _context = context;
    }

    public async Task<IssuerEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Issuers
            .AsNoTracking()
            .Include(i => i.PortalCredentials)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<IssuerEntity?> GetByCnpjAsync(Cnpj cnpj, CancellationToken cancellationToken = default)
    {
        return await _context.Issuers
            .AsNoTracking()
            .Include(i => i.PortalCredentials)
            .FirstOrDefaultAsync(i => i.Cnpj == cnpj, cancellationToken);
    }

    public async Task<IEnumerable<IssuerEntity>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Issuers
            .AsNoTracking()
            .Where(i => i.IsActive)
            .Include(i => i.PortalCredentials)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(IssuerEntity issuer, CancellationToken cancellationToken = default)
    {
        await _context.Issuers.AddAsync(issuer, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(IssuerEntity issuer, CancellationToken cancellationToken = default)
    {
        issuer.UpdatedAt = DateTime.UtcNow;
        _context.Issuers.Update(issuer);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Cnpj cnpj, CancellationToken cancellationToken = default)
    {
        return await _context.Issuers
            .AsNoTracking()
            .AnyAsync(i => i.Cnpj == cnpj, cancellationToken);
    }
    
}