using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceMicroservice.Infrastructure.Services;

/// <summary>
/// Real IPM NFSe API client.
/// Implements multipart/form-data transport, Basic Auth, cookie management, and XML signature.
/// </summary>
public class NationalApiClient : IApiClient
{
    private readonly ILogger<NationalApiClient> _logger;
    private readonly IPortalCredentialsRepository _credentialsRepo;
    private readonly PortalConfigs _portalConfigs;


    public NationalApiClient(
        ILogger<NationalApiClient> logger,
        IPortalCredentialsRepository credentialsRepo,
        IOptions<PortalConfigs> configs)
    {
        _logger = logger;
        _credentialsRepo = credentialsRepo;
        _portalConfigs = configs.Value;
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
                // In test mode, accept any certificate issues
                if (isTestMode)
                {
                    if (errors != SslPolicyErrors.None)
                        _logger.LogWarning("Test mode: accepting certificate with errors: {Errors}", errors);
                    return true;
                }
                // In production, only accept valid certificates
                return errors == SslPolicyErrors.None;
            };

        var config = _portalConfigs.GetConfig(credentials.PortalType);
        if (config == null)
            throw new InvalidOperationException($"No portal configuration found for portal type {credentials.PortalType}");

        var baseUrl = config.ApiBaseUrl;

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
        };

        var dpsXmlGZipB64 = GZipAndBase64Encode(xml);

        var requestBody = new
        {
            dpsXmlGZipB64
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // var endpoint = "/SefinNacional/nfse";
        var endpoint = config.Endpoints.EmitInvoice;
        var apiUrl = new Uri(httpClient.BaseAddress!, endpoint);
        _logger.LogInformation(
        "Submitting DPS to National API - CNPJ: {IssuerCnpj}, TestMode: {TestMode}, URL: {ApiUrl}",
        issuerCnpj,
        isTestMode,
        apiUrl);

        if (isTestMode)
        {
            // saving dpsXmlGZipB64 to file for debugging
            var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "national-xml-output");
            Directory.CreateDirectory(outputDirectory);
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, $"dpsXmlGZipB64-{issuerCnpj}-{DateTime.UtcNow:yyyyMMddHHmmss}.txt"), dpsXmlGZipB64);
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, $"finalXml-{issuerCnpj}-{DateTime.UtcNow:yyyyMMddHHmmss}.xml"), xml);
        }

        try
        {
            var response = await httpClient.PostAsync(endpoint, content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation(
                "Sending DPS submission request to National API at {ApiUrl}",
                apiUrl);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.BadRequest ||
                    response.StatusCode == HttpStatusCode.Forbidden ||
                    response.StatusCode == HttpStatusCode.InternalServerError)
                {
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<NationalNfseErrorResponse>(responseContent);
                        if (errorResponse != null && errorResponse.Erros.Count > 0)
                        {
                            _logger.LogWarning(
                                "National API returned errors: {Errors}",
                                string.Join(", ", errorResponse.Erros));
                        }
                        return new NfseSubmissionResult
                        {
                            Success = false,
                            Protocol = null,
                            Messages = errorResponse?.Erros.Select(e => $"{e.Codigo}: {e.Descricao}").ToList() ?? new List<string>(),
                            RawResponse = responseContent,
                        };
                    }
                    catch (JsonException)
                    {
                        throw new HttpRequestException(
                            $"National API returned {response.StatusCode}: {responseContent}");
                    }
                }
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

            var submissionResponse = JsonSerializer.Deserialize<NationalNfseSuccessResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (submissionResponse == null)
            {
                _logger.LogError("Failed to deserialize National API response: {Response}", responseContent);
                throw new InvalidOperationException("Invalid response from National API");
            }
            if (submissionResponse.Alertas != null && submissionResponse.Alertas.Count > 0)
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
                Protocol = null, // Parse protocol from responseContent if available
                RawResponse = responseContent,
                ChaveAcesso = submissionResponse.ChaveAcesso,
                PdfUrl = BuildConsultaPublicaUrl(baseUrl, submissionResponse.ChaveAcesso),
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

    /// <summary>
    /// The submission API (SEFIN) never returns a document link — only the signed XML and a
    /// chaveAcesso. The DANFSe itself lives on the separate public consultation portal
    /// ("NFS-e Via"), which anyone (including the end customer) can use to view/download it,
    /// no login required: https://www.gov.br/nfse/pt-br/nfs-e-via/links
    /// Mirrors whichever environment the submission's own ApiBaseUrl points at (homologação
    /// vs produção), rather than trusting isTestMode alone, so the link always matches where
    /// the invoice was actually sent.
    /// </summary>
    private static string BuildConsultaPublicaUrl(string apiBaseUrl, string chaveAcesso)
    {
        var isHomologacao = apiBaseUrl.Contains("producaorestrita", StringComparison.OrdinalIgnoreCase);
        var host = isHomologacao ? "producaorestrita.via.nfse.gov.br" : "via.nfse.gov.br";
        return $"https://{host}/consultapublica/{chaveAcesso}";
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

public class NationalNfseSuccessResponse : NationalNfseResponse
{
    public string ChaveAcesso { get; set; } = null!;
    public string nfseXmlGZipB64 { get; set; } = null!;
    public List<MensagemProcessamento> Alertas { get; set; } = new();
}
public class NationalNfseErrorResponse : NationalNfseResponse
{
    public List<MensagemProcessamento> Erros { get; set; } = new();
}

public enum TipoAmbiente
{
    Producao = 1,
    Homologacao = 2
}

public abstract class NationalNfseResponse
{
    public TipoAmbiente TipoAmbiente { get; set; }
    public string VersaoAplicativo { get; set; } = null!;
    public DateTime DataHoraProcessamento { get; set; }
    public string IdDps { get; set; } = null!;
}

public class MensagemProcessamento
{
    public string Mensagem { get; set; } = null!;
    public string Codigo { get; set; } = null!;
    public string Descricao { get; set; } = null!;
    public string Complemento { get; set; } = null!;
}