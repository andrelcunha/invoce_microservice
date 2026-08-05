namespace InvoiceMicroservice.Domain.Entities;

public enum InvoiceEmissionJobStatus
{
    Pending = 0,
    Processing = 1,
    Succeeded = 2,
    FailedRetryable = 3,
    FailedPermanent = 4
}

public class InvoiceEmissionJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string IssuerCnpj { get; set; } = default!;
    public string PayloadJson { get; set; } = default!; // serialized EmitInvoiceCommand input
    public InvoiceEmissionJobStatus Status { get; set; } = InvoiceEmissionJobStatus.Pending;

    public int Attempts { get; set; } = 0;
    public int MaxAttempts { get; set; } = 5;
    public DateTime? NextRetryAt { get; set; }

    public DateTime? LockedAt { get; set; }
    public string? LockedBy { get; set; }

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}