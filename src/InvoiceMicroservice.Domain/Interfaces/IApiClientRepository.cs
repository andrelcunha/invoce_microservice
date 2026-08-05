using InvoiceMicroservice.Domain.Entities;

namespace InvoiceMicroservice.Domain.Interfaces;

public interface IApiClientRepository
{
    Task<ApiClient?> GetByApiKeyHashAsync(string apiKeyHash, CancellationToken ct = default);
}
