using System;

namespace InvoiceMicroservice.Infrastructure.Xml;

public class TaxConfig
{
    public decimal PIbsUf { get; set; } = 0.10m;
    public decimal PRedAliqUf { get; set; } = 0.00m;
    public decimal PIbsMun { get; set; } = 0.00m;
    public decimal PRedAliqMun { get; set; } = 0.00m;
    public decimal PCbs { get; set; } = 0.90m;
    public decimal PRedAliqCbs { get; set; } = 0.00m;
}

