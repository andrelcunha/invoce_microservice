namespace InvoiceMicroservice.Domain.Interfaces;
/// <summary>
/// Client for interacting with IPM Emissor Nacional API.
/// Handles NFS-e submission, query, and cancellation operations.
/// </summary>
public interface IIpmClient
{
    /// <summary>
    /// Submits an NFS-e XML to IPM for emission.
    /// </summary>
    /// <param name="xml">Complete NFS-e XML document</param>
    /// <param name="isTestMode">Whether to use test environment (default: true)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response containing protocol number, PDF URL, and validation messages</returns>
    Task<IpmSubmissionResult> SubmitInvoiceAsync(
        string xml, 
        bool isTestMode = true, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the status of a previously submitted NFS-e.
    /// </summary>
    /// <param name="protocol">IPM protocol number from submission</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current status and invoice details</returns>
    Task<IpmQueryResult> QueryInvoiceAsync(
        string protocol, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a previously issued NFS-e.
    /// </summary>
    /// <param name="invoiceNumber">IPM invoice number</param>
    /// <param name="cancellationReason">Reason for cancellation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cancellation confirmation</returns>
    Task<IpmCancellationResult> CancelInvoiceAsync(
        string invoiceNumber, 
        string cancellationReason, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of NFS-e submission to IPM.
/// </summary>
public record IpmSubmissionResult
{
    /// <summary>
    /// Whether submission was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// IPM protocol number for tracking.
    /// </summary>
    public string? Protocol { get; init; }

    /// <summary>
    /// Issued invoice number (when successful).
    /// </summary>
    public string? InvoiceNumber { get; init; }

    /// <summary>
    /// Verification code for consumer validation.
    /// </summary>
    public string? VerificationCode { get; init; }

    /// <summary>
    /// URL to download issued invoice PDF.
    /// </summary>
    public string? PdfUrl { get; init; }

    /// <summary>
    /// Validation messages or error descriptions.
    /// </summary>
    public List<string> Messages { get; init; } = new();

    /// <summary>
    /// Raw XML/JSON response from IPM (for audit).
    /// </summary>
    public string? RawResponse { get; init; }
}

/// <summary>
/// Result of invoice status query.
/// </summary>
public record IpmQueryResult
{
    public bool Found { get; init; }
    public string? Status { get; init; }
    public string? InvoiceNumber { get; init; }
    public DateTime? IssuedAt { get; init; }
    public string? PdfUrl { get; init; }
}

/// <summary>
/// Result of invoice cancellation.
/// </summary>
public record IpmCancellationResult
{
    public bool Success { get; init; }
    public string? CancellationProtocol { get; init; }
    public List<string> Messages { get; init; } = new();
}