using System;

namespace InvoiceMicroservice.Infrastructure.Xml;

public class TaxConfig
{
    // IBS/CBS rates (as decimals, e.g., 0.05 = 5%)
    public decimal PIbsUf { get; set; }
    public decimal PIbsMun { get; set; }
    public decimal PCbs { get; set; }
    
    // Reduction rates for IBS/CBS
    public decimal PRedAliqUf { get; set; }
    public decimal PRedAliqMun { get; set; }
    public decimal PRedAliqCbs { get; set; }
    
    // PIS/COFINS rates (as decimals, e.g., 0.0065 = 0.65%)
    public decimal PAliquotaPis { get; set; }
    public decimal PAliquotaCofins { get; set; }
}

