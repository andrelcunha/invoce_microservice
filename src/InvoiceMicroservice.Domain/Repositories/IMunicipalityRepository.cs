using InvoiceMicroservice.Domain.Entities;

namespace InvoiceMicroservice.Domain.Repositories;

public interface IMunicipalityRepository
{
    /// <summary>
    /// Get municipality by IBGE code (7-digit)
    /// </summary>
    Task<Municipality?> GetByIbgeCodeAsync(string ibgeCode, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get municipality by TOM code (IPM code)
    /// </summary>
    Task<Municipality?> GetByTomCodeAsync(string tomCode, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get municipality by name and UF (case-insensitive)
    /// </summary>
    Task<Municipality?> GetByCityAndUfAsync(string cityName, string uf, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all municipalities for a given state
    /// </summary>
    Task<List<Municipality>> GetByUfAsync(string uf, CancellationToken cancellationToken = default);
}
