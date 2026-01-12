using InvoiceMicroservice.Domain.Entities;

namespace InvoiceMicroservice.Domain.Interfaces;

public interface IPortalCredentialsRepository
{
    Task<PortalCredentials?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PortalCredentials?> GetByIssuerCnpjAsync(string issuerCnpj, CancellationToken cancellationToken = default);
    Task<IEnumerable<PortalCredentials>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task AddAsync(PortalCredentials credentials, CancellationToken cancellationToken = default);
    Task UpdateAsync(PortalCredentials credentials, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}