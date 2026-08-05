using InvoiceMicroservice.Domain.Entities;

namespace InvoiceMicroservice.Domain.Interfaces;

public interface IPortalCredentialsRepository
{
    Task<PortalCredentialsEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PortalCredentialsEntity?> GetByIssuerCnpjAsync(string issuerCnpj, CancellationToken cancellationToken = default);
    Task<IEnumerable<PortalCredentialsEntity>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task AddAsync(PortalCredentialsEntity credentials, CancellationToken cancellationToken = default);
    Task UpdateAsync(PortalCredentialsEntity credentials, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}