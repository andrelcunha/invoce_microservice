using InvoiceMicroservice.Domain.Entities;

namespace InvoiceMicroservice.Infrastructure.Xml;

// Helper class to encapsulate service tax codes
internal record ServiceTypeTaxCodes
{
    public string NbsCode { get; init; }
    public string ServiceListCode { get; init; }
    public string OperationIndicator { get; init; }
    public string TaxSituationCode { get; init; }
    // public string TaxClassificationCode { get; init; }
    public string Description { get; init; } 

    public ServiceTypeTaxCodes(ServiceTypeTaxMapping mapping)
    {
        NbsCode = mapping.NbsCode;
        ServiceListCode = mapping.ServiceListCode;
        OperationIndicator = mapping.OperationIndicator;
        TaxSituationCode = mapping.TaxSituationCode;
        // TaxClassificationCode = mapping.TaxClassificationCode;
        Description = mapping.Description;
    }

    private ServiceTypeTaxCodes(
        string nbsCode,
        string serviceListCode,
        string operationIndicator,
        string taxSituationCode,
        // string taxClassificationCode, 
        string description)
    {
        NbsCode = nbsCode;
        ServiceListCode = serviceListCode;
        OperationIndicator = operationIndicator;
        TaxSituationCode = taxSituationCode;
        // TaxClassificationCode = taxClassificationCode;
        Description = description;
    }

    public static ServiceTypeTaxCodes Default() => new(
        "118032900",
        "140101",
        "050101",
        "000",
        // "000001",
        "Lubrificação, limpeza, lustração, revisão, carga e recarga, conserto, restauração, blindagem, manutenção e conservação de máquinas, veículos, aparelhos, equipamentos, motores, elevadores ou de qualquer objeto (exceto peças e partes empregadas, que ficam sujeitas ao ICMS)." );
}
