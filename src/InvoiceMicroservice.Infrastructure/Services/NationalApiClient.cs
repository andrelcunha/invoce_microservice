using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using InvoiceMicroservice.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace InvoiceMicroservice.Infrastructure.Services;

/// <summary>
/// Real IPM NFSe API client.
/// Implements multipart/form-data transport, Basic Auth, cookie management, and XML signature.
/// </summary>
public class NationalApiClient : IApiClient
{
    private readonly ILogger<NationalApiClient> _logger;
    private readonly IPortalCredentialsRepository _credentialsRepo;


    public NationalApiClient(
        ILogger<NationalApiClient> logger,
        ApiClientOptions options,
        IPortalCredentialsRepository credentialsRepo)
    {
        _logger = logger;
        _credentialsRepo = credentialsRepo;
    }

    public async Task<NfseSubmissionResult> SubmitInvoiceAsync(
        string xml,
        string issuerCnpj,
        bool isTestMode = true,
        CancellationToken cancellationToken = default)
    {
        var credentials = await _credentialsRepo.GetByIssuerCnpjAsync(issuerCnpj, cancellationToken);
        if (credentials == null)
            throw new InvalidOperationException($"No credentials found for issuer CNPJ {issuerCnpj}");

        // Load certificate for client authentication
        var cert = X509CertificateLoader.LoadPkcs12(
            credentials.CertificateData!,
            credentials.CertificatePasswordHash!,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(cert);

        handler.ServerCertificateCustomValidationCallback =
            (message, cert, chain, errors) =>
            {
                if (isTestMode && errors == SslPolicyErrors.None)
                    return true;
                if (!isTestMode && errors == SslPolicyErrors.RemoteCertificateNameMismatch)
                {
                    _logger.LogWarning("Accepting test environment certificate with name mismatch");
                    return true;
                }
                return errors == SslPolicyErrors.None;
            };

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(credentials.ApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(60)
        };

        var dpsXmlGZipB64 = GZipAndBase64Encode(xml);

        var requestBody = new
        {
            dpsXmlGZipB64
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _logger.LogDebug(
            "Submitting DPS to National API - CNPJ: {IssuerCnpj}, TestMode: {TestMode}",
            issuerCnpj,
            isTestMode);
        
        if (isTestMode)
        {
            // saving dpsXmlGZipB64 to file for debugging
            var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "national-xml-output");
            Directory.CreateDirectory(outputDirectory);
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, $"dpsXmlGZipB64-{issuerCnpj}-{DateTime.UtcNow:yyyyMMddHHmmss}.txt"), dpsXmlGZipB64);
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, $"finalXml-{issuerCnpj}-{DateTime.UtcNow:yyyyMMddHHmmss}.xml"), xml);
            // return new NfseSubmissionResult
            // {
            //     Success = false,
            //     Messages = [$"API client GZIP + Base64 encoding demo mode - not sending request."]
            // };
        }

        try
        {
            var response = await httpClient.PostAsync("/nfse", content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "National API returned error status {StatusCode}: {Response}",
                    response.StatusCode,
                    responseContent);
                throw new HttpRequestException(
                    $"National API returned {response.StatusCode}: {responseContent}");
            }

            _logger.LogInformation(
                "DPS submitted successfully - Response: {Response}",
                responseContent);

            var submissionResponse = JsonSerializer.Deserialize<NationalNfseSubmissionResponse>(responseContent);
            if (submissionResponse == null)
            {
                _logger.LogError("Failed to deserialize National API response: {Response}", responseContent);
                throw new InvalidOperationException("Invalid response from National API");
            }
            if (submissionResponse.Alertas.Count > 0)
            {
                _logger.LogWarning(
                    "National API returned alerts: {Alerts}",
                    string.Join(", ", submissionResponse.Alertas));
            }
            if (string.IsNullOrEmpty(submissionResponse.ChaveAcesso))
            {
                _logger.LogError("National API response missing ChaveAcesso: {Response}", responseContent);
                throw new InvalidOperationException("Invalid response from National API: missing ChaveAcesso");
            }
            if (string.IsNullOrEmpty(submissionResponse.nfseXmlGZipB64))
            {
                _logger.LogError("National API response missing nfseXmlGZipB64: {Response}", responseContent);
                throw new InvalidOperationException("Invalid response from National API: missing nfseXmlGZipB64");
            }
            var nfseXml = Base64DecodeAndGunzip(submissionResponse.nfseXmlGZipB64);
            await WriteToFileAsync(issuerCnpj, nfseXml);

            var result = new NfseSubmissionResult
            {
                Success = true,
                Protocol = null // Parse protocol from responseContent if available
            };

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error submitting DPS to National API");
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout submitting DPS to National API");
            throw new TimeoutException("Request to National API timed out", ex);
        }
    }

    public static string GZipAndBase64Encode(string xml)
    {
        byte[] xmlBytes = Encoding.UTF8.GetBytes(xml);
        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
        {
            gzipStream.Write(xmlBytes, 0, xmlBytes.Length);
        }
        var compressedBytes = outputStream.ToArray();
        return Convert.ToBase64String(compressedBytes);
    }


    // create a method to decode base64 and gunzip
    public static string Base64DecodeAndGunzip(string base64Gzip)
    {
        byte[] compressedBytes = Convert.FromBase64String(base64Gzip);
        using var inputStream = new MemoryStream(compressedBytes);
        using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        gzipStream.CopyTo(outputStream);
        byte[] decompressedBytes = outputStream.ToArray();
        return Encoding.UTF8.GetString(decompressedBytes);
    }

    public Task<InvoiceQueryResult> QueryInvoiceAsync(string protocol, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<InvoiceCancellationResult> CancelInvoiceAsync(string invoiceNumber, string cancellationReason, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private async Task WriteToFileAsync(string issuerCnpj, string xml)
    {
        // saving nfseXmlGZipB64 to file for debugging
        var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "national-xml-output");
        Directory.CreateDirectory(outputDirectory);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, $"nfseXml-{issuerCnpj}-{DateTime.UtcNow:yyyyMMddHHmmss}.xml"), xml);
    }
}

public class NationalNfseSubmissionResponse
{
    public TipoAmbiente TipoAmbiente { get; set; }
    public string VersaoAplicativo { get; set; } = null!;
    public DateTime DataHoraProcessamento { get; set; }
    public string IdDps { get; set; } = null!;
    public string ChaveAcesso { get; set; } = null!;
    public string nfseXmlGZipB64 { get; set; } = null!;
    public List<string> Alertas { get; set; } = new();
}

public enum TipoAmbiente
{
    Producao = 1,
    Homologacao = 2
}