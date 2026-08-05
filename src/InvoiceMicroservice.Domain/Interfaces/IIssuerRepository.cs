using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.ValueObjects;

namespace InvoiceMicroservice.Domain.Interfaces;

public interface IIssuerRepository
{
    Task<IssuerEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IssuerEntity?> GetByCnpjAsync(Cnpj cnpj, CancellationToken cancellationToken = default);
    Task<IEnumerable<IssuerEntity>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task AddAsync(IssuerEntity issuer, CancellationToken cancellationToken = default);
    Task UpdateAsync(IssuerEntity issuer, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Cnpj cnpj, CancellationToken cancellationToken = default);
}