# IPM NFSe API – Integration Guide (Language-Agnostic)

This document describes how to connect to the IPM municipal NFSe API used by municipalities such as Concórdia/SC. It is language-agnostic and focuses on HTTP transport, authentication, session management, XML payload requirements, digital signature, and operational guidance.

## 1) Endpoints & Transport
- Protocol: HTTPS only.
- Method: POST.
- Content type: `multipart/form-data` with a single part named `xml` (file-like content, `filename=invoice.xml`, `Content-Type: text/xml`).
- Base URL (Concórdia/SC default):
  - `https://concordia.atende.net/?pg=rest&service=WNERestServiceNFSe&cidade=padrao`
- Municipality selection:
  - The `cidade` context is established by cookie and/or query parameter on the endpoint.
  - The XML must also include the municipal TOM code for the provider (`prestador/cidade`).

## 2) Authentication
- Scheme: HTTP Basic Authentication.
- Header: `Authorization: Basic <base64(username:password)>`.
- Provide valid IPM-issued credentials per municipality/tenant.

## 3) Session & Cookies
- The API establishes a session via `Set-Cookie` headers (commonly `PHPSESSID` and `cidade`).
- Client behavior:
  - Capture all `Set-Cookie` values on the first response.
  - Include them on subsequent requests using the `Cookie` header, e.g. `Cookie: PHPSESSID=...; cidade=...`.
  - Maintain cookies per logical client/tenant to avoid cross-session leakage.

## 4) Request Format (multipart)
- Boundary/multipart handling must be correct; most HTTP clients will set this automatically when sending a file part.
- Single part named `xml`:
  - `filename`: `invoice.xml` (recommendation)
  - `Content-Type`: `text/xml`
  - Body: the full NFSe XML document as described below.

## 5) XML Payload Structure (Concórdia/IPM variations)
Root element: `<nfse>`

Common sections and fields (names as expected by IPM for Concórdia; other municipalities may vary slightly):

- Optional test flag:
  - `<nfse_teste>1</nfse_teste>` when running in test/validation mode.

- `<nf>` (invoice totals and basic data):
  - `<data_fato_gerador>`: Date in `DD/MM/YYYY`.
  - `<valor_total>`: Decimal using comma as separator (e.g. `100,00`).
  - `<valor_desconto>`, `<valor_ir>`, `<valor_inss>`, `<valor_contribuicao_social>`, `<valor_rps>`, `<valor_pis>`, `<valor_cofins>`: Usually `0,00` unless applicable.
  - `<observacao>`: Free text (apply XML escaping rules below).

- `<prestador>` (service provider/issuer):
  - `<cpfcnpj>`: Numeric only.
  - `<cidade>`: Municipal TOM code (e.g. `8083` for Concórdia/SC).

- `<tomador>` (service consumer):
  - `<endereco_informado>`: `1` when address provided.
  - `<tipo>`: `J` (juridical), `F` (physical), `E` (foreigner).
  - For `tipo=E`, include: `<identificador>`, `<estado>`, `<pais>`.
  - Common fields: `<cpfcnpj>`, `<ie>` (optional), `<nome_razao_social>`, `<sobrenome_nome_fantasia>`, `<logradouro>`, `<email>`, `<numero_residencia>`, `<complemento>`, `<ponto_referencia>`, `<bairro>`, `<cidade>` (TOM code for the consumer’s city), `<cep>`.
  - Optional phones when available: `<ddd_fone_comercial>`, `<fone_comercial>`, `<ddd_fone_residencial>`, `<fone_residencial>`, `<ddd_fax>`, `<fone_fax>`.

- `<itens>` → `<lista>` (service items):
  - `<tributa_municipio_prestador>`: `S` or `N`.
  - `<codigo_local_prestacao_servico>`: TOM code (normally the provider’s city TOM).
  - `<unidade_codigo>`: e.g. `1`.
  - `<unidade_quantidade>`: e.g. `1`.
  - `<unidade_valor_unitario>`: Decimal with comma.
  - `<codigo_item_lista_servico>`: Municipal service code (e.g. `1401`).
  - `<descritivo>`: Service description.
  - `<aliquota_item_lista_servico>`: Percentage with comma (e.g. `5,00`).
  - `<situacao_tributaria>`: Municipal tax situation (e.g. `00`).
  - `<valor_tributavel>`, `<valor_deducao>`, `<valor_issrf>` as applicable.

- `<forma_pagamento>`:
  - `<tipo_pagamento>`: e.g. `1` (à vista).

XML escaping (per municipality manual):
- Replace `&` → `&amp;`, `<` → `&lt;`, `>` → `&gt;`, `'` → `&apos;`, `"` → `&quot;`.
- Remove `/` if not allowed by the local rules.

TOM code notes:
- Provide correct TOM (Tabela de Órgãos e Municípios) code for both provider (`prestador/cidade`) and, when applicable, consumer (`tomador/cidade`).
- Example mapping (subject to change): Concórdia/SC → `8083`.

## 6) Digital Signature (when required)
Some municipalities require an enveloped XML Digital Signature:
- Standard: W3C XMLDSig (enveloped signature inside the XML document).
- Algorithms:
  - Signature: `http://www.w3.org/2000/09/xmldsig#rsa-sha1`.
  - Canonicalization: `http://www.w3.org/TR/2001/REC-xml-c14n-20010315`.
- Certificate: X.509 from a PFX (PKCS#12) file; provide file path and password.
- Placement: Enveloped signature (i.e., a `<Signature>` element embedded in the XML). The exact reference/ID may vary by municipality; follow the local NFSe spec if they require a specific reference URI.

## 7) Response Format
- Transport-level status may be `< 500` even on business errors. Always parse the XML body.
- Typical response root: `<retorno>` with fields:
  - `<sucesso>`: `true`/`false` (string values).
  - `<mensagem>`: Human-readable status or error.
  - `<numero_nfse>`: External invoice number when successful.
  - `<cod_verificador_autenticidade>`: Authenticity/verification code (when issued).
  - `<link_pdf>`: Link to the printable PDF (when issued).
  - Additional fields: `<data_emissao>`, situation codes/descriptions, etc.
- Success criteria: `sucesso=true` and a valid `<numero_nfse>`.

## 8) Error Handling & Retries
- Parse `<sucesso>` and `<mensagem>` for business outcome regardless of HTTP status.
- Recommended client-side timeout: 30 seconds per request.
- Retry policy (client-side):
  - Retry network/timeouts and transient 5xx conditions with exponential backoff.
  - Do not retry validation/business-rule errors.
- Dead-letter logging is recommended for failed submissions after max retries.

## 9) Example (cURL)
Submit a signed or unsigned XML (depending on municipal requirement):

```bash
curl -X POST \
  'https://concordia.atende.net/?pg=rest&service=WNERestServiceNFSe&cidade=padrao' \
  -H 'Authorization: Basic BASE64_USERNAME_PASSWORD' \
  -F 'xml=@invoice.xml;type=text/xml;filename=invoice.xml' \
  -i
```

- On the first successful response, capture all `Set-Cookie` headers and reuse them:

```bash
curl -X POST \
  'https://concordia.atende.net/?pg=rest&service=WNERestServiceNFSe&cidade=padrao' \
  -H 'Authorization: Basic BASE64_USERNAME_PASSWORD' \
  -H 'Cookie: PHPSESSID=...; cidade=...' \
  -F 'xml=@invoice.xml;type=text/xml;filename=invoice.xml' \
  -i
```

Example success payload (illustrative):

```xml
<?xml version="1.0" encoding="UTF-8"?>
<retorno>
  <sucesso>true</sucesso>
  <mensagem>Nota fiscal emitida com sucesso</mensagem>
  <numero_nfse>123456</numero_nfse>
  <cod_verificador_autenticidade>ABCDEF12345</cod_verificador_autenticidade>
  <link_pdf>https://concordia.atende.net/nfse/123456.pdf</link_pdf>
</retorno>
```

## 10) Configuration & Environment Variables
Below are typical configuration keys used by clients of the Concórdia/IPM API. Names shown match one reference implementation; adapt names to your environment as needed.

Municipal API client settings:
- `CONCORDIA_API_URL`: Base URL (default: `https://concordia.atende.net/?pg=rest&service=WNERestServiceNFSe&cidade=padrao`).
- `CONCORDIA_API_USERNAME`: Basic Auth username.
- `CONCORDIA_API_PASSWORD`: Basic Auth password.
- `CONCORDIA_API_TEST_MODE`: `true|false` – when `true`, include `<nfse_teste>1</nfse_teste>` in XML.
- `CONCORDIA_REQUIRES_SIGNATURE`: `true|false` – whether to sign the XML.
- `CONCORDIA_PFX_PATH`: Filesystem path to the PFX certificate (when signing).
- `CONCORDIA_PFX_PASSWORD`: Password for the PFX certificate.

Issuer (service provider) defaults (populate the `<prestador>` section and may be used as fallbacks):
- `ISSUER_CNPJ`: CNPJ (digits or masked; will be normalized to digits).
- `ISSUER_MUNICIPAL_INSCRIPTION`: Municipal registration.
- `ISSUER_CNAE`: CNAE code.
- `ISSUER_NAME`: Corporate name.
- `ISSUER_STREET`, `ISSUER_NUMBER`, `ISSUER_NEIGHBORHOOD`, `ISSUER_CITY`, `ISSUER_UF`, `ISSUER_ZIP_CODE`.

Service defaults (populate `<itens>/<lista>`):
- `DEFAULT_SERVICO_CODE`: Municipal service code (e.g. `1401`).
- `DEFAULT_SERVICE_DESCRIPTION`: Service description text.
- `DEFAULT_ALIQUOTA`: Percentage (e.g. `5.00`).
- `DEFAULT_SITUACAO_TRIBUTARIA`: e.g. `00`.

Operational controls:
- `MUNICIPAL_API_TIMEOUT`: Request timeout in ms (default `30000`).
- `MUNICIPAL_API_RETRY_ATTEMPTS`: Max retry attempts for transient failures (default `3`).
- `MUNICIPAL_API_MOCK_MODE`: `true|false` – when `true`, client may generate XML locally without sending (for development only; not recognized by IPM servers).

## 11) Validation & Data Rules (high level)
- CPF/CNPJ: Numeric-only in XML (remove punctuation).
- Monetary values: Use comma as decimal separator in XML fields expected by the municipality.
- Dates: `DD/MM/YYYY` where required.
- TOM codes: Provide valid codes for the municipality.
- Foreign customers (`tipo=E`): Include identifier/state/country.

## 12) Security Considerations
- Always use HTTPS; validate server certificates.
- Protect credentials used for Basic Auth.
- Store PFX certificate files securely; restrict filesystem permissions.
- Never log full XML with personal data in production; redact sensitive fields.

## 13) Troubleshooting
- Received HTTP 3xx with no body: Follow redirects only if explicitly required; default behavior may be to avoid redirects. Re-submit to the canonical endpoint.
- Business error with 200/4xx: Inspect `<sucesso>` and `<mensagem>`; correct data and retry if applicable.
- Missing `numero_nfse`: Treat as failure; check `<mensagem>` and municipal rules.
- Cookie not persisted: Ensure `Set-Cookie` headers are stored and returned on subsequent requests.
- Signature rejected: Verify algorithm (RSA-SHA1), certificate validity/chain, and enveloped signature format.

---