using Microsoft.EntityFrameworkCore;
using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Infrastructure.Data;

namespace InvoiceMicroservice.Infrastructure.Repositories;

public class ServiceTypeTaxMappingRepository : IServiceTypeTaxMappingRepository
{
    private readonly InvoiceDbContext _context;

    public ServiceTypeTaxMappingRepository(InvoiceDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceTypeTaxMapping?> GetByServiceTypeKeyAsync(
        string serviceTypeKey, 
        CancellationToken cancellationToken = default)
    {
        return await _context.ServiceTypeTaxMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ServiceTypeKey == serviceTypeKey && x.IsActive, cancellationToken);
    }

    public async Task<ServiceTypeTaxMapping?> GetByCnaeCodeAsync(
        string cnaeCode, 
        CancellationToken cancellationToken = default)
    {
        return await _context.ServiceTypeTaxMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CnaeCode == cnaeCode && x.IsActive, cancellationToken);
    }

    public async Task<List<ServiceTypeTaxMapping>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ServiceTypeTaxMappings
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Description)
            .ToListAsync(cancellationToken);
    }
}

