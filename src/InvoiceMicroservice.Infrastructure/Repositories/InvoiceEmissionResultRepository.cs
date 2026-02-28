using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceMicroservice.Infrastructure.Repositories;

public sealed class InvoiceEmissionResultRepository(InvoiceDbContext db) : IInvoiceEmissionResultRepository
{
    public Task<InvoiceEmissionResult?> GetByJobIdAsync(Guid jobId, CancellationToken ct = default)
    {
        return db.InvoiceEmissionResults
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.JobId == jobId, ct);
    }

    public async Task UpsertByJobIdAsync(InvoiceEmissionResult result, CancellationToken ct = default)
    {
        var existing = await db.InvoiceEmissionResults
            .FirstOrDefaultAsync(x => x.JobId == result.JobId, ct);

        if (existing is null)
        {
            db.InvoiceEmissionResults.Add(result);
        }
        else
        {
            existing.IssuerCnpj = result.IssuerCnpj;
            existing.PortalType = result.PortalType;
            existing.IssuedAt = result.IssuedAt;
            existing.ProviderProcessedAt = result.ProviderProcessedAt;
            existing.NumeroDfe = result.NumeroDfe;
            existing.SerieDfe = result.SerieDfe;
            existing.CodStatus = result.CodStatus;
            existing.StatusDescription = result.StatusDescription;
            existing.Protocolo = result.Protocolo;
            existing.ChaveAcesso = result.ChaveAcesso;
            existing.VerificationCode = result.VerificationCode;
            existing.DocumentUrl = result.DocumentUrl;
            existing.RequestXml = result.RequestXml;
            existing.ResponseRaw = result.ResponseRaw;
            existing.AlertsJson = result.AlertsJson;
            existing.ErrorMessage = result.ErrorMessage;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}