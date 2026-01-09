using InvoiceMicroservice.Domain.Entities;

namespace InvoiceMicroservice.Domain.Interfaces;

public interface IInvoiceXmlBuilder
{
    Task<string> BuildInvoiceXmlAsync(Invoice invoice, bool isTestMode = true, CancellationToken cancellationToken = default);
}
