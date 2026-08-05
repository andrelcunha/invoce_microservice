using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Interfaces;
using InvoiceMicroservice.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceMicroservice.Infrastructure.Services;

/// <summary>
/// Real IPM NFSe API client.
/// Implements multipart/form-data transport, Basic Auth, cookie management, and XML signature.
/// </summary>
public class IpmApiClient : IApiClient
{
    private readonly ILogger<IpmApiClient> _logger;
    private readonly IPortalCredentialsRepository _credentialsRepo;
    private readonly PortalConfigs _portalConfigs;

    private readonly CookieContainer _cookieContainer;
    private readonly HttpClient _httpClient;

    public IpmApiClient(
        ILogger<IpmApiClient> logger,
        IOptions<PortalConfigs> portalConfigs,
        IPortalCredentialsRepository credentialsRepo)
    {
        _logger = logger;
        _credentialsRepo = credentialsRepo;
        _portalConfigs = portalConfigs.Value;
        _cookieContainer = new CookieContainer();
        _httpClient = InitializeHttpClient();
    }

    private HttpClient InitializeHttpClient()
    {
        var handler = new HttpClientHandler
        {
            UseCookies = false, // Manual cookie management
            AllowAutoRedirect = false // Per integration guide, avoid redirects
        };
        return new HttpClient(handler);
    }

    private void ConfigureHttpClient(PortalCredentialsEntity credentials, PortalConfig config)
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);

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

        // string? issuerCnpj = GetIssuerCnpjFromXml(xml);
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

        var config = _portalConfigs.GetConfig(credentials.PortalType);
        if (config == null)
            throw new InvalidOperationException($"No API configuration found for portal type {credentials.PortalType}");
        ConfigureHttpClient(credentials, config);


        while (attempt < config.RetryAttempts)
        {
            attempt++;

            try
            {
                _logger.LogInformation(
                    "Submitting invoice (attempt {Attempt}/{MaxAttempts}, testMode: {TestMode})",
                    attempt,
                    config.RetryAttempts,
                    isTestMode);

                // Sign XML if required
                var finalXml = credentials.RequiresSignature
                    ? SignXml(xml, credentials.CertificateData!, credentials.CertificatePasswordHash!)
                    : xml;

                // Build multipart/form-data request
                using var content = new MultipartFormDataContent();
                var xmlContent = new ByteArrayContent(Encoding.UTF8.GetBytes(finalXml));
                xmlContent.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
                content.Add(xmlContent, "xml", "invoice.xml");

                // get the base url from configuration instead of getting it from credentials

                // Include cookies from previous session
                // var baseUrl = credentials.ApiBaseUrl;
                var baseUrl = config.ApiBaseUrl;
                var endpoint = config.Endpoints.EmitInvoice;
                var fullUrl = new Uri(new Uri(baseUrl), endpoint);
                var request = new HttpRequestMessage(HttpMethod.Post, fullUrl)
                {
                    Content = content
                };
                AddCookiesToRequest(request, baseUrl);

                // Send request
                var response = await _httpClient.SendAsync(request, cancellationToken);

                // Capture cookies for subsequent requests
                CaptureCookiesFromResponse(response, baseUrl);

                // Read response respecting the XML encoding declaration (IPM returns ISO-8859-1)
                using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var responseDoc = XDocument.Load(responseStream);
                var responseXml = responseDoc.ToString();

                _logger.LogDebug("IPM response (HTTP {StatusCode}): {ResponseXml}",
                    (int)response.StatusCode,
                    responseXml);

                // Parse response (success determined by XML content, not HTTP status)
                return ParseResponse(responseDoc);
            }
            catch (TaskCanceledException ex)
            {
                lastException = ex;
                _logger.LogWarning(
                    ex,
                    "IPM request timeout (attempt {Attempt}/{MaxAttempts})",
                    attempt,
                    config.RetryAttempts);

                if (attempt < config.RetryAttempts)
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
                    config.RetryAttempts);

                if (attempt < config.RetryAttempts)
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
                    config.RetryAttempts);

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

    public async Task<InvoiceQueryResult> QueryInvoiceAsync(
        string protocol,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("QueryInvoiceAsync not implemented for IPM API. Protocol: {Protocol}", protocol);

        return await Task.FromResult(new InvoiceQueryResult
        {
            Found = false,
            Status = "Query operation not available in current IPM API implementation"
        });
    }

    public async Task<InvoiceCancellationResult> CancelInvoiceAsync(
        string invoiceNumber,
        string cancellationReason,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "CancelInvoiceAsync not implemented for IPM API. Invoice: {InvoiceNumber}, Reason: {Reason}",
            invoiceNumber,
            cancellationReason);

        return await Task.FromResult(new InvoiceCancellationResult
        {
            Success = false,
            Messages = new List<string> { "Cancellation operation not available in current IPM API implementation" }
        });
    }

    private string SignXml(string xml, byte[] certificateData, string certificatePassword)
    {
        var certificate = X509CertificateLoader.LoadPkcs12(
            certificateData,
            certificatePassword,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

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

    private NfseSubmissionResult ParseResponse(XDocument doc)
    {
        var responseXml = doc.ToString();
        try
        {
            var root = doc.Root;

            if (root == null)
            {
                return new NfseSubmissionResult
                {
                    Success = false,
                    Messages = new List<string> { "Empty response from IPM" },
                    RawResponse = responseXml
                };
            }

            // IPM actual response structure (observed):
            //   <retorno>
            //     <mensagem><codigo>NFS-e válida para emissão.</codigo></mensagem>
            //     <numero_nfse>1</numero_nfse>
            //     <situacao_codigo_nfse>1</situacao_codigo_nfse>      (1 = Emitida)
            //     <situacao_descricao_nfse>Emitida</situacao_descricao_nfse>
            //     <cod_verificador_autenticidade>...</cod_verificador_autenticidade>
            //     <link_nfse>...</link_nfse>
            //   </retorno>
            // Note: <sucesso> is not present in observed responses — use situacao_codigo_nfse instead.
            var mensagem = root.Element("mensagem")?.Element("codigo")?.Value
                        ?? root.Element("mensagem")?.Value
                        ?? "";
            var numeroNfse = root.Element("numero_nfse")?.Value;
            var situacaoCodigo = root.Element("situacao_codigo_nfse")?.Value;
            var situacaoDescricao = root.Element("situacao_descricao_nfse")?.Value;
            var codVerificador = root.Element("cod_verificador_autenticidade")?.Value;
            var linkNfse = root.Element("link_nfse")?.Value ?? root.Element("link_pdf")?.Value;

            // Fallback: honour <sucesso> if present (may appear in some portal versions)
            var sucessoStr = root.Element("sucesso")?.Value;
            bool success;
            if (sucessoStr is not null)
                success = sucessoStr.Equals("true", StringComparison.OrdinalIgnoreCase);
            else
                success = situacaoCodigo == "1" || !string.IsNullOrEmpty(numeroNfse);

            var messages = new List<string>();
            if (!string.IsNullOrEmpty(mensagem))
                messages.Add(mensagem);
            if (!string.IsNullOrEmpty(situacaoDescricao) && situacaoDescricao != mensagem)
                messages.Add(situacaoDescricao);

            foreach (var msgElement in root.Descendants("erro").Concat(root.Descendants("aviso")))
            {
                var code = msgElement.Element("codigo")?.Value;
                var desc = msgElement.Element("descricao")?.Value ?? msgElement.Value;
                messages.Add($"[{code}] {desc}");
            }

            var result = new NfseSubmissionResult
            {
                Success = success,
                Protocol = numeroNfse,
                InvoiceNumber = numeroNfse,
                VerificationCode = codVerificador,
                PdfUrl = linkNfse,
                Messages = messages,
                RawResponse = responseXml
            };

            if (success)
                _logger.LogInformation(
                    "IPM submission successful. Invoice: {InvoiceNumber}, Verification: {VerificationCode}",
                    numeroNfse, codVerificador);
            else
                _logger.LogWarning("IPM submission failed. Messages: {Messages}",
                    string.Join("; ", messages));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse IPM response: {ResponseXml}", responseXml);
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
}
