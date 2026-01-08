using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using InvoiceMicroservice.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace InvoiceMicroservice.Infrastructure.Services;

/// <summary>
/// Real IPM NFSe API client.
/// Implements multipart/form-data transport, Basic Auth, cookie management, and XML signature.
/// </summary>
public class IpmApiClient : IIpmClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IpmApiClient> _logger;
    private readonly IpmApiClientOptions _options;
    private readonly CookieContainer _cookieContainer;

    public IpmApiClient(
        HttpClient httpClient,
        ILogger<IpmApiClient> logger,
        IpmApiClientOptions options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options;
        _cookieContainer = new CookieContainer();

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        // Basic Authentication
        var authBytes = Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}");
        var authHeader = Convert.ToBase64String(authBytes);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        _logger.LogInformation(
            "IpmApiClient configured. BaseUrl: {BaseUrl}, Timeout: {Timeout}s, Signature: {RequiresSignature}",
            _options.BaseUrl,
            _options.TimeoutSeconds,
            _options.RequiresSignature);
    }

    public async Task<IpmSubmissionResult> SubmitInvoiceAsync(
        string xml,
        bool isTestMode = true,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _options.RetryAttempts)
        {
            attempt++;

            try
            {
                _logger.LogInformation(
                    "Submitting invoice to IPM (attempt {Attempt}/{MaxAttempts}, testMode: {TestMode})",
                    attempt,
                    _options.RetryAttempts,
                    isTestMode);

                // Sign XML if required
                var finalXml = _options.RequiresSignature
                    ? SignXml(xml)
                    : xml;

                // Build multipart/form-data request
                using var content = new MultipartFormDataContent();
                var xmlContent = new ByteArrayContent(Encoding.UTF8.GetBytes(finalXml));
                xmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
                content.Add(xmlContent, "xml", "invoice.xml");

                // Include cookies from previous session
                var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl)
                {
                    Content = content
                };
                AddCookiesToRequest(request);

                // Send request
                var response = await _httpClient.SendAsync(request, cancellationToken);

                // Capture cookies for subsequent requests
                CaptureCookiesFromResponse(response);

                // Read response body
                var responseXml = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogDebug("IPM response (HTTP {StatusCode}): {ResponseXml}",
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
                    "IPM request timeout (attempt {Attempt}/{MaxAttempts})",
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
                    "IPM request failed (attempt {Attempt}/{MaxAttempts})",
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
                    "Unexpected error submitting invoice to IPM (attempt {Attempt}/{MaxAttempts})",
                    attempt,
                    _options.RetryAttempts);

                // Don't retry on unexpected errors
                break;
            }
        }

        // All retries exhausted
        return new IpmSubmissionResult
        {
            Success = false,
            Messages = new List<string>
            {
                $"Failed after {attempt} attempts: {lastException?.Message ?? "Unknown error"}"
            }
        };
    }

    public async Task<IpmQueryResult> QueryInvoiceAsync(
        string protocol,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("QueryInvoiceAsync not implemented for IPM API. Protocol: {Protocol}", protocol);

        return await Task.FromResult(new IpmQueryResult
        {
            Found = false,
            Status = "Query operation not available in current IPM API implementation"
        });
    }

    public async Task<IpmCancellationResult> CancelInvoiceAsync(
        string invoiceNumber,
        string cancellationReason,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "CancelInvoiceAsync not implemented for IPM API. Invoice: {InvoiceNumber}, Reason: {Reason}",
            invoiceNumber,
            cancellationReason);

        return await Task.FromResult(new IpmCancellationResult
        {
            Success = false,
            Messages = new List<string> { "Cancellation operation not available in current IPM API implementation" }
        });
    }

    private string SignXml(string xml)
    {
        try
        {
            if (string.IsNullOrEmpty(_options.CertificatePath))
            {
                throw new InvalidOperationException("Certificate path required for XML signing but not configured");
            }

            // Load certificate from PFX
            var certificate = new X509Certificate2(
                _options.CertificatePath,
                _options.CertificatePassword,
                X509KeyStorageFlags.Exportable);

            // Load XML document
            var xmlDoc = new XmlDocument { PreserveWhitespace = true };
            xmlDoc.LoadXml(xml);

            // Create signed XML
            var signedXml = new SignedXml(xmlDoc)
            {
                SigningKey = certificate.GetRSAPrivateKey()
            };

            // Reference the entire document
            var reference = new Reference { Uri = "" };
            reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            reference.AddTransform(new XmlDsigC14NTransform());
            signedXml.AddReference(reference);

            // Add key info
            var keyInfo = new KeyInfo();
            keyInfo.AddClause(new KeyInfoX509Data(certificate));
            signedXml.KeyInfo = keyInfo;

            // Compute signature
            signedXml.ComputeSignature();

            // Append signature to XML
            var signatureElement = signedXml.GetXml();
            xmlDoc.DocumentElement?.AppendChild(xmlDoc.ImportNode(signatureElement, true));

            _logger.LogDebug("XML signed successfully using certificate: {Thumbprint}", certificate.Thumbprint);

            return xmlDoc.OuterXml;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign XML");
            throw new InvalidOperationException("XML signature failed", ex);
        }
    }

    private IpmSubmissionResult ParseResponse(string responseXml)
    {
        try
        {
            var doc = XDocument.Parse(responseXml);
            var root = doc.Root;

            if (root == null)
            {
                return new IpmSubmissionResult
                {
                    Success = false,
                    Messages = new List<string> { "Empty response from IPM" },
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

            var result = new IpmSubmissionResult
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
                    "IPM submission successful. Invoice: {InvoiceNumber}, Verification: {VerificationCode}",
                    numeroNfse,
                    codVerificador);
            }
            else
            {
                _logger.LogWarning(
                    "IPM submission failed. Messages: {Messages}",
                    string.Join("; ", messages));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse IPM response: {ResponseXml}", responseXml);

            return new IpmSubmissionResult
            {
                Success = false,
                Messages = new List<string> { $"Response parsing error: {ex.Message}" },
                RawResponse = responseXml
            };
        }
    }

    private void AddCookiesToRequest(HttpRequestMessage request)
    {
        var cookies = _cookieContainer.GetCookies(new Uri(_options.BaseUrl));
        if (cookies.Count > 0)
        {
            var cookieHeader = string.Join("; ", cookies.Cast<Cookie>().Select(c => $"{c.Name}={c.Value}"));
            request.Headers.Add("Cookie", cookieHeader);

            _logger.LogDebug("Including cookies in request: {CookieHeader}", cookieHeader);
        }
    }

    private void CaptureCookiesFromResponse(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            foreach (var header in setCookieHeaders)
            {
                _cookieContainer.SetCookies(new Uri(_options.BaseUrl), header);
                _logger.LogDebug("Captured cookie: {SetCookieHeader}", header);
            }
        }
    }
}

/// <summary>
/// Configuration options for IPM API client.
/// </summary>
public record IpmApiClientOptions
{
    public required string BaseUrl { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    public int RetryAttempts { get; init; } = 3;
    public bool RequiresSignature { get; init; }
    public string? CertificatePath { get; init; }
    public string? CertificatePassword { get; init; }
}