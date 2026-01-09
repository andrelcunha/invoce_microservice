using InvoiceMicroservice.Domain.Entities;

namespace InvoiceMicroservice.Domain.Interfaces;

public interface IPortalCredentialsRepository
{
    Task<PortalCredentials?> GetByIssuerCnpjAsync(string issuerCnpj, CancellationToken cancellationToken = default);
    Task AddAsync(PortalCredentials credentials, CancellationToken cancellationToken = default);
    Task UpdateAsync(PortalCredentials credentials, CancellationToken cancellationToken = default);
}