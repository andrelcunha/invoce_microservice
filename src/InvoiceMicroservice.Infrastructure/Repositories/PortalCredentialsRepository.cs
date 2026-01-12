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

    public async Task<PortalCredentials?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PortalCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<PortalCredentials?> GetByIssuerCnpjAsync(
        string issuerCnpj, 
        CancellationToken cancellationToken = default)
    {
        return await _context.PortalCredentials
            .Include(c => c.Municipality) // Eager load municipality for factory
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IssuerCnpj == issuerCnpj && c.IsActive, cancellationToken);
    }

    public async Task<IEnumerable<PortalCredentials>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.PortalCredentials.AsNoTracking();
        
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }
        
        return await query.OrderBy(c => c.IssuerCnpj).ToListAsync(cancellationToken);
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

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var credentials = await _context.PortalCredentials.FindAsync(new object[] { id }, cancellationToken);
        if (credentials is not null)
        {
            credentials.Deactivate();
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}