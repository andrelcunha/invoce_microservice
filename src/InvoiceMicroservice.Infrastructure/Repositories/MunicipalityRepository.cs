using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceMicroservice.Infrastructure.Repositories;

public class MunicipalityRepository : IMunicipalityRepository
{
    private readonly InvoiceDbContext _context;

    public MunicipalityRepository(InvoiceDbContext context)
    {
        _context = context;
    }

    public async Task<Municipality?> GetByIbgeCodeAsync(string ibgeCode, CancellationToken cancellationToken = default)
    {
        return await _context.Municipalities
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.IbgeCode == ibgeCode, cancellationToken);
    }

    public async Task<Municipality?> GetByTomCodeAsync(string tomCode, CancellationToken cancellationToken = default)
    {
        return await _context.Municipalities
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TomCode == tomCode, cancellationToken);
    }

    public async Task<Municipality?> GetByCityAndUfAsync(string cityName, string uf, CancellationToken cancellationToken = default)
    {
        return await _context.Municipalities
            .AsNoTracking()
            .FirstOrDefaultAsync(m => 
                EF.Functions.ILike(m.Name, cityName) && m.Uf == uf.ToUpperInvariant(), 
                cancellationToken);
    }

    public async Task<List<Municipality>> GetByUfAsync(string uf, CancellationToken cancellationToken = default)
    {
        return await _context.Municipalities
            .AsNoTracking()
            .Where(m => m.Uf == uf.ToUpperInvariant())
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);
    }
}
