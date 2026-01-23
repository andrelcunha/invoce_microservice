using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace InvoiceMicroservice.Infrastructure.Services;

/// <summary>
/// Real IPM NFSe API client.
/// Implements multipart/form-data transport, Basic Auth, cookie management, and XML signature.
/// </summary>
public class NationalApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NationalApiClient> _logger;
    private readonly IPortalCredentialsRepository _credentialsRepo;
    private readonly ApiClientOptions _options;

    private readonly CookieContainer _cookieContainer;

    public NationalApiClient(
        HttpClient httpClient,
        ILogger<NationalApiClient> logger,
        ApiClientOptions options,
        IPortalCredentialsRepository credentialsRepo)
    {
        _options = options;
        _httpClient = httpClient;
        _logger = logger;
        _credentialsRepo = credentialsRepo;
        _cookieContainer = new CookieContainer();
    }

    private void ConfigureHttpClient(PortalCredentials credentials)
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        // Basic Authentication
        var authBytes = Encoding.UTF8.GetBytes($"{credentials.Username}:{credentials.PasswordHash}");
        var authHeader = Convert.ToBase64String(authBytes);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
    }

    public async Task<NfseSubmissionResult> SubmitInvoiceAsync(
        string xml,
        string issuerCnpj,
        bool isTestMode = true,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        Exception? lastException = null;

        if (string.IsNullOrEmpty(issuerCnpj))
        {
            return new NfseSubmissionResult
            {
                Success = false,
                Messages = new List<string> { "Cannot extract issuer CNPJ from XML" }
            };
        }

        var credentials = await _credentialsRepo.GetByIssuerCnpjAsync(issuerCnpj, cancellationToken);
        if (credentials == null)
        {
            return new NfseSubmissionResult
            {
                Success = false,
                Messages = new List<string> { $"No IPM credentials configured for CNPJ {issuerCnpj}" }
            };
        }
        ConfigureHttpClient(credentials);


        while (attempt < _options.RetryAttempts)
        {
            attempt++;

            try
            {
                _logger.LogInformation(
                    "Submitting invoice (attempt {Attempt}/{MaxAttempts}, testMode: {TestMode})",
                    attempt,
                    _options.RetryAttempts,
                    isTestMode);

                // Compress
                var dpsXmlGZipB64 = CompressXmlToBase64(xml);
                var json =  JsonSerializer.Serialize(new { dpsXmlGZipB64 = dpsXmlGZipB64 });


                if (isTestMode)
                {
                    // saving dpsXmlGZipB64 to file for debugging
                    var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "ipm-xml-output");
                    Directory.CreateDirectory(outputDirectory);
                    await File.WriteAllTextAsync(Path.Combine(outputDirectory, $"dpsXmlGZipB64-{issuerCnpj}-{DateTime.UtcNow:yyyyMMddHHmmss}.txt"), dpsXmlGZipB64);
                    await File.WriteAllTextAsync(Path.Combine(outputDirectory, $"finalXml-{issuerCnpj}-{DateTime.UtcNow:yyyyMMddHHmmss}.xml"), xml);
                    return new NfseSubmissionResult
                    {
                        Success = false,
                        Messages = [$"API client GZIP + Base64 encoding demo mode - not sending request."]
                    };
                }
                // Build multipart/form-data request
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var xmlContent = new ByteArrayContent(Encoding.UTF8.GetBytes(xml));
                xmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/xml");

                // Include cookies from previous session
                var baseUrl = credentials.ApiBaseUrl;
                var request = new HttpRequestMessage(HttpMethod.Post, baseUrl)
                {
                    Content = content
                };
                AddCookiesToRequest(request, baseUrl);

                // Send request
                var response = await _httpClient.SendAsync(request, cancellationToken);

                // Capture cookies for subsequent requests
                CaptureCookiesFromResponse(response, baseUrl);

                // Read response body
                var responseXml = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogDebug("National API response (HTTP {StatusCode}): {ResponseXml}",
                    (int)response.StatusCode,
                    responseXml);

                // Parse response (success determined by XML content, not HTTP status)
                return ParseResponse(responseXml);
            }
            catch (TaskCanceledException ex)
            {
                lastException = ex;
                _logger.LogWarning(
                    ex,
                    "National API request timeout (attempt {Attempt}/{MaxAttempts})",
                    attempt,
                    _options.RetryAttempts);

                if (attempt < _options.RetryAttempts)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // Exponential backoff
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                _logger.LogWarning(
                    ex,
                    "National API request failed (attempt {Attempt}/{MaxAttempts})",
                    attempt,
                    _options.RetryAttempts);

                if (attempt < _options.RetryAttempts)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogError(
                    ex,
                    "Unexpected error submitting invoice to National API (attempt {Attempt}/{MaxAttempts})",
                    attempt,
                    _options.RetryAttempts);

                // Don't retry on unexpected errors
                break;
            }
        }

        // All retries exhausted
        return new NfseSubmissionResult
        {
            Success = false,
            Messages = new List<string>
            {
                $"Failed after {attempt} attempts: {lastException?.Message ?? "Unknown error"}"
            }
        };
    }

    // private static string? GetIssuerCnpjFromXml(string xml)
    // {
    //     var doc = XDocument.Parse(xml);
    //     var issuerCnpj = doc.Root?.Element("prestador")?.Element("cpfcnpj")?.Value;
    //     return issuerCnpj;
    // }

    public async Task<InvoiceQueryResult> QueryInvoiceAsync(
        string protocol,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("QueryInvoiceAsync not implemented for National API. Protocol: {Protocol}", protocol);

        return await Task.FromResult(new InvoiceQueryResult
        {
            Found = false,
            Status = "Query operation not available in current National API implementation"
        });
    }

    public async Task<InvoiceCancellationResult> CancelInvoiceAsync(
        string invoiceNumber,
        string cancellationReason,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "CancelInvoiceAsync not implemented for National API. Invoice: {InvoiceNumber}, Reason: {Reason}",
            invoiceNumber,
            cancellationReason);

        return await Task.FromResult(new InvoiceCancellationResult
        {
            Success = false,
            Messages = new List<string> { "Cancellation operation not available in current National API implementation" }
        });
    }

    private NfseSubmissionResult ParseResponse(string responseXml)
    {
        try
        {
            var doc = XDocument.Parse(responseXml);
            var root = doc.Root;

            if (root == null)
            {
                return new NfseSubmissionResult
                {
                    Success = false,
                    Messages = new List<string> { "Empty response from National API" },
                    RawResponse = responseXml
                };
            }

            // Parse <retorno> structure per integration guide
            var sucessoStr = root.Element("sucesso")?.Value ?? "false";
            var mensagem = root.Element("mensagem")?.Value ?? "";
            var numeroNfse = root.Element("numero_nfse")?.Value;
            var codVerificador = root.Element("cod_verificador_autenticidade")?.Value;
            var linkPdf = root.Element("link_pdf")?.Value;

            var success = sucessoStr.Equals("true", StringComparison.OrdinalIgnoreCase);

            var messages = new List<string>();
            if (!string.IsNullOrEmpty(mensagem))
            {
                messages.Add(mensagem);
            }

            // Check for additional error/warning messages
            foreach (var msgElement in root.Descendants("erro").Concat(root.Descendants("aviso")))
            {
                var code = msgElement.Element("codigo")?.Value;
                var desc = msgElement.Element("descricao")?.Value ?? msgElement.Value;
                messages.Add($"[{code}] {desc}");
            }

            var result = new NfseSubmissionResult
            {
                Success = success,
                Protocol = numeroNfse, // IPM uses numero_nfse as protocol/identifier
                InvoiceNumber = numeroNfse,
                VerificationCode = codVerificador,
                PdfUrl = linkPdf,
                Messages = messages,
                RawResponse = responseXml
            };

            if (success)
            {
                _logger.LogInformation(
                    "National API submission successful. Invoice: {InvoiceNumber}, Verification: {VerificationCode}",
                    numeroNfse,
                    codVerificador);
            }
            else
            {
                _logger.LogWarning(
                    "National API submission failed. Messages: {Messages}",
                    string.Join("; ", messages));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse National API response: {ResponseXml}", responseXml);

            return new NfseSubmissionResult
            {
                Success = false,
                Messages = new List<string> { $"Response parsing error: {ex.Message}" },
                RawResponse = responseXml
            };
        }
    }

    private void AddCookiesToRequest(HttpRequestMessage request, string baseUrl)
    {
        var cookies = _cookieContainer.GetCookies(new Uri(baseUrl));
        if (cookies.Count > 0)
        {
            var cookieHeader = string.Join("; ", cookies.Cast<Cookie>().Select(c => $"{c.Name}={c.Value}"));
            request.Headers.Add("Cookie", cookieHeader);

            _logger.LogDebug("Including cookies in request: {CookieHeader}", cookieHeader);
        }
    }

    private void CaptureCookiesFromResponse(HttpResponseMessage response, string baseUrl)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            foreach (var header in setCookieHeaders)
            {
                _cookieContainer.SetCookies(new Uri(baseUrl), header);
                _logger.LogDebug("Captured cookie: {SetCookieHeader}", header);
            }
        }
    }

        /// <summary>
    /// Compacta uma string XML em GZIP e retorna os bytes.
    /// </summary>
    public static byte[] CompressToGzip(string xml) 
    { 
        byte[] xmlBytes = Encoding.UTF8.GetBytes(xml);
        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
        {
            gzipStream.Write(xmlBytes, 0, xmlBytes.Length);
        }
        return outputStream.ToArray();
    }

        /// <summary>
    /// Converte bytes GZIP para base64.
    /// </summary>
    public static string ToBase64(byte[] gzipBytes)
    {
        return Convert.ToBase64String(gzipBytes);
    }

    /// <summary>
    /// Compacta uma string XML em GZIP e retorna a string base64.
    /// </summary>
    public static string CompressXmlToBase64(string xml)
    { 
        var gzipBytes = CompressToGzip(xml);
        return ToBase64(gzipBytes);
    }
}