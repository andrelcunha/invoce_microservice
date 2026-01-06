namespace InvoiceMicroservice.Domain.Entities;

/// <summary>
/// Maps service types to their required tax codes for IPM NFS-e emission.
/// Each CNAE typically has a corresponding NBS code and tax classification.
/// </summary>
public class ServiceTypeTaxMapping
{
    public int Id { get; private set; }
    
    /// <summary>
    /// Unique key to identify this service type (e.g., "vehicle-wash-45200-05")
    /// </summary>
    public string ServiceTypeKey { get; private set; } = null!;
    
    /// <summary>
    /// CNAE code in format NNNN-N/NN (e.g., "45.20-0-05")
    /// </summary>
    public string CnaeCode { get; private set; } = null!;
    
    /// <summary>
    /// Human-readable description of the service
    /// </summary>
    public string Description { get; private set; } = null!;
    
    /// <summary>
    /// NBS - Nomenclatura Brasileira de Serviços (9 digits)
    /// Required for tax reform compliance
    /// </summary>
    public string NbsCode { get; private set; } = null!;
    
    /// <summary>
    /// Service list code from LC 116/2003 (format: N.NN or NN.NN)
    /// </summary>
    public string ServiceListCode { get; private set; } = null!;
    
    /// <summary>
    /// Operation indicator code (6 digits) - links to service classification
    /// </summary>
    public string OperationIndicator { get; private set; } = null!;
    
    /// <summary>
    /// Tax situation code (3 digits) - depends on issuer's tax regime
    /// </summary>
    public string TaxSituationCode { get; private set; } = null!;
    
    /// <summary>
    /// Tax classification code (6 digits) - must match operation indicator
    /// </summary>
    public string TaxClassificationCode { get; private set; } = null!;
    
    /// <summary>
    /// Whether this mapping is currently active
    /// </summary>
    public bool IsActive { get; private set; } = true;
    
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // EF Core constructor
    private ServiceTypeTaxMapping() { }

    public static ServiceTypeTaxMapping Create(
        string serviceTypeKey,
        string cnaeCode,
        string description,
        string nbsCode,
        string serviceListCode,
        string operationIndicator,
        string taxSituationCode,
        string taxClassificationCode)
    {
        return new ServiceTypeTaxMapping
        {
            ServiceTypeKey = serviceTypeKey,
            CnaeCode = cnaeCode,
            Description = description,
            NbsCode = nbsCode,
            ServiceListCode = serviceListCode,
            OperationIndicator = operationIndicator,
            TaxSituationCode = taxSituationCode,
            TaxClassificationCode = taxClassificationCode,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}