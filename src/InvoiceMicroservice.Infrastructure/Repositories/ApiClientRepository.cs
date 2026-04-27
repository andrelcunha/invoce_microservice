using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceMicroservice.Infrastructure.Repositories;

public class ApiClientRepository : IApiClientRepository
{
    private readonly InvoiceDbContext _db;

    public ApiClientRepository(InvoiceDbContext db)
    {
        _db = db;
    }

    public Task<ApiClient?> GetByApiKeyHashAsync(string apiKeyHash, CancellationToken ct = default)
        => _db.ApiClients
                    .FirstOrDefaultAsync(c => c.ApiKeyHash == apiKeyHash && c.IsActive, ct);
}
