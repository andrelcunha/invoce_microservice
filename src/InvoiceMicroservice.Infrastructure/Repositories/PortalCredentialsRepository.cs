using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Domain.ValueObjects;
using InvoiceMicroservice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvoiceMicroservice.Infrastructure.Repositories;

public class PortalCredentialsRepository : IPortalCredentialsRepository
{
    private readonly InvoiceDbContext _context;
    private readonly ILogger<PortalCredentialsRepository> _logger;

    public PortalCredentialsRepository(InvoiceDbContext context, ILogger<PortalCredentialsRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PortalCredentialsEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PortalCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<PortalCredentialsEntity?> GetByIssuerCnpjAsync(
        string issuerCnpj, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Buscando credenciais do portal para emissor com CNPJ {Cnpj}", issuerCnpj);
        var cnpj = new Cnpj(issuerCnpj);
        return await _context.PortalCredentials
            .AsNoTracking()
            .Include(c => c.Issuer)
            .FirstOrDefaultAsync(c => c.Issuer.Cnpj == cnpj && c.IsActive, cancellationToken);
    }

    public async Task<IEnumerable<PortalCredentialsEntity>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.PortalCredentials.AsNoTracking();
        
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }
        
        return await query.OrderBy(c => c.Issuer.Cnpj).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(PortalCredentialsEntity credentials, CancellationToken cancellationToken = default)
    {
        await _context.PortalCredentials.AddAsync(credentials, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PortalCredentialsEntity credentials, CancellationToken cancellationToken = default)
    {
        _context.PortalCredentials.Update(credentials);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var credentials = await _context.PortalCredentials.FindAsync([id], cancellationToken);
        if (credentials is not null)
        {
            credentials.Deactivate();
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}