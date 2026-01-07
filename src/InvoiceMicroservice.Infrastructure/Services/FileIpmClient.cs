using System.Xml.Linq;
using InvoiceMicroservice.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace InvoiceMicroservice.Infrastructure.Services;

/// <summary>
/// Dummy IPM client that writes XML to files instead of calling the API.
/// Useful for development and XML validation before connecting to real IPM.
/// </summary>
public class FileIpmClient : IIpmClient
{
    private readonly string _outputDirectory;
    private readonly ILogger<FileIpmClient> _logger;

    public FileIpmClient(ILogger<FileIpmClient> logger, string? outputDirectory = null)
    {
        _logger = logger;
        _outputDirectory = outputDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), "ipm-xml-output");
        
        // Create output directory if it doesn't exist
        Directory.CreateDirectory(_outputDirectory);
        
        _logger.LogInformation("FileIpmClient initialized. XML files will be saved to: {OutputDirectory}", _outputDirectory);
    }

    public Task<IpmSubmissionResult> SubmitInvoiceAsync(
        string xml, 
        bool isTestMode = true, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Parse XML to extract identifier
            var doc = XDocument.Parse(xml);
            var identifier = doc.Root?.Element("identificador")?.Value ?? Guid.NewGuid().ToString("N");
            var testSuffix = isTestMode ? "_TEST" : "";
            
            // Generate filename with timestamp
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var filename = $"nfse_{identifier}_{timestamp}{testSuffix}.xml";
            var filepath = Path.Combine(_outputDirectory, filename);
            
            // Write formatted XML to file
            var formattedXml = XDocument.Parse(xml).ToString();
            File.WriteAllText(filepath, formattedXml);
            
            _logger.LogInformation(
                "NFS-e XML saved to file: {Filepath} (TestMode: {TestMode})", 
                filepath, 
                isTestMode);
            
            // Simulate successful response
            var result = new IpmSubmissionResult
            {
                Success = true,
                Protocol = $"DUMMY-{identifier[..8]}",
                InvoiceNumber = $"NFS-{Random.Shared.Next(1000, 9999)}",
                VerificationCode = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                PdfUrl = $"file://{filepath}",
                Messages = new List<string> 
                { 
                    "XML successfully saved to file (DUMMY MODE)",
                    $"File location: {filepath}",
                    isTestMode ? "Test mode: NFS-e NOT issued" : "Production mode simulation"
                },
                RawResponse = $"<!-- File-based dummy response -->\n{formattedXml}"
            };
            
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save NFS-e XML to file");
            
            var errorResult = new IpmSubmissionResult
            {
                Success = false,
                Messages = new List<string> { $"File write error: {ex.Message}" }
            };
            
            return Task.FromResult(errorResult);
        }
    }

    public Task<IpmQueryResult> QueryInvoiceAsync(
        string protocol, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("QueryInvoiceAsync called on dummy implementation. Protocol: {Protocol}", protocol);
        
        return Task.FromResult(new IpmQueryResult
        {
            Found = false,
            // Messages = new List<string> { "Dummy implementation - query not available" }
        });
    }

    public Task<IpmCancellationResult> CancelInvoiceAsync(
        string invoiceNumber, 
        string cancellationReason, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "CancelInvoiceAsync called on dummy implementation. Invoice: {InvoiceNumber}, Reason: {Reason}", 
            invoiceNumber, 
            cancellationReason);
        
        return Task.FromResult(new IpmCancellationResult
        {
            Success = false,
            Messages = new List<string> { "Dummy implementation - cancellation not available" }
        });
    }
}