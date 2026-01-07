namespace InvoiceMicroservice.Domain.Entities;

/// <summary>
/// Brazilian municipality with IBGE and TOM codes for tax document emission.
/// </summary>
public class Municipality
{
    public int Id { get; private set; }
    
    /// <summary>
    /// Official IBGE 7-digit code (e.g., 4204301 for Concórdia-SC)
    /// </summary>
    public string IbgeCode { get; private set; } = null!;
    
    /// <summary>
    /// Municipality name in UTF-8
    /// </summary>
    public string Name { get; private set; } = null!;
    
    /// <summary>
    /// Two-letter state code (e.g., SC, RS, SP)
    /// </summary>
    public string Uf { get; private set; } = null!;
    
    /// <summary>
    /// TOM code for IPM NFS-e (e.g., 8083 for Concórdia-SC)
    /// </summary>
    public string TomCode { get; private set; } = null!;
    
    /// <summary>
    /// Date when municipality was officially created (ISO format)
    /// </summary>
    public string? CreatedAt { get; private set; }
    
    /// <summary>
    /// Date when municipality code was extinguished (ISO format), if applicable
    /// </summary>
    public string? ExtinguishedAt { get; private set; }

    // EF Core constructor
    private Municipality() { }

    public static Municipality Create(
        string ibgeCode,
        string name,
        string uf,
        string tomCode,
        string? createdAt = null,
        string? extinguishedAt = null)
    {
        return new Municipality
        {
            IbgeCode = ibgeCode,
            Name = name,
            Uf = uf,
            TomCode = tomCode,
            CreatedAt = createdAt,
            ExtinguishedAt = extinguishedAt
        };
    }
}
