using InvoiceMicroservice.Domain.Entities;

namespace InvoiceMicroservice.Domain.Interfaces;

public interface IServiceTypeTaxMappingRepository
{
    Task<ServiceTypeTaxMapping?> GetByServiceTypeKeyAsync(string serviceTypeKey, CancellationToken ct = default);
    Task <ServiceTypeTaxMapping?> GetByCnaeCodeAsync(string cnaeCode, CancellationToken ct = default);
    Task<List<ServiceTypeTaxMapping>> GetAllActiveAsync(CancellationToken ct = default);
}