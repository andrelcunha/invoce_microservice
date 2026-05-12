# Invoice Microservice — Specifications

This document is the source of truth for external contracts this service must honour: portal protocols, XML schemas, tax rules, and regulatory notes. Implementation decisions live in `DESIGN.md`.

---

## 1. Portals

The service supports two NFS-e portals, selected per issuer via `PortalCredentials.PortalType`:

| Portal | Builder | Client | Used by |
|---|---|---|---|
| `IPM` | `IpmXmlBuilder` | `IpmApiClient` | Municipalities using IPM system (e.g. Concórdia/SC) |
| `Nacional` | `NationalXmlBuilder` | `NationalApiClient` | Municipalities on the federal NFS-e Nacional system |

---

## 2. IPM Portal

### 2.1 Transport

- Protocol: HTTPS POST only
- Content-Type: `multipart/form-data` — single part named `xml` (`filename=invoice.xml`, `Content-Type: text/xml`)
- Base URL example (Concórdia/SC): `https://concordia.atende.net/?pg=rest&service=WNERestServiceNFSe&cidade=padrao`
- Municipality context established by cookie and/or `cidade` query parameter

### 2.2 Authentication

- HTTP Basic Auth: `Authorization: Basic <base64(username:password)>`
- Credentials are per municipality/tenant, stored in `portal_credentials` table

### 2.3 Session & Cookies

- First response sets `PHPSESSID` and `cidade` cookies via `Set-Cookie`
- All subsequent requests must echo those cookies in `Cookie` header
- Maintain cookies per issuer/tenant to avoid cross-session leakage

### 2.4 XML Structure

Root element: `<nfse>`

```xml
<nfse>
  <nfse_teste>1</nfse_teste>        <!-- 1 = test/homologation, omit for production -->
  <identificador>UNIQUE_ID</identificador>
  <nf>...</nf>
  <prestador>...</prestador>
  <tomador>...</tomador>
  <itens>...</itens>
  <IBSCBS>...</IBSCBS>              <!-- at <nfse> level, NOT inside <nf> -->
  <forma_pagamento>...</forma_pagamento>
</nfse>
```

#### `<nf>` — Invoice totals

```xml
<nf>
  <data_fato_gerador>DD/MM/YYYY</data_fato_gerador>  <!-- service date, NOT emission date -->
  <valor_total>1500,00</valor_total>                 <!-- comma as decimal separator -->
  <valor_desconto>0,00</valor_desconto>
  <valor_ir>0,00</valor_ir>
  <valor_inss>0,00</valor_inss>
  <valor_contribuicao_social>0,00</valor_contribuicao_social>
  <valor_rps>0,00</valor_rps>
  <pis_cofins>
    <cst>01</cst>                   <!-- 2 digits, zero-padded -->
    <tipo_retencao>2</tipo_retencao> <!-- DEPRECATED by IPM — portal infers from valor_pis/valor_cofins presence -->
    <base_calculo>1500,00</base_calculo>
    <aliquota_pis>0,65</aliquota_pis>
    <aliquota_cofins>3,00</aliquota_cofins>
  </pis_cofins>
  <!-- valor_pis / valor_cofins: fill ONLY when PIS/COFINS is retained (retido).
       For B2C PassouLavou (não retido): leave these empty.
       Filling both <pis_cofins> AND <valor_pis>/<valor_cofins> simultaneously is a double-count bug. -->
  <observacao>Service description</observacao>
</nf>
```

#### `<prestador>` — Service provider (issuer)

```xml
<prestador>
  <cpfcnpj>00000000000000</cpfcnpj>  <!-- 14 digits, no punctuation -->
  <cidade>8083</cidade>               <!-- TOM code for provider's municipality -->
</prestador>
```

#### `<tomador>` — Service consumer

```xml
<tomador>
  <endereco_informado>1</endereco_informado>
  <tipo>J</tipo>   <!-- J=Juridical, F=Physical, E=Foreign -->
  <cpfcnpj>00000000000000</cpfcnpj>
  <nome_razao_social>Company Name</nome_razao_social>
  <logradouro>Street</logradouro>
  <numero_residencia>123</numero_residencia>
  <complemento>Apt</complemento>
  <bairro>Neighborhood</bairro>
  <cidade>8083</cidade>   <!-- TOM code for consumer's municipality -->
  <cep>89000000</cep>
  <email>email@example.com</email>
</tomador>
```

For `tipo=E` (foreign): also include `<identificador>`, `<estado>`, `<pais>`.

#### `<itens>/<lista>` — Service items

```xml
<itens>
  <lista>
    <tributa_municipio_prestador>S</tributa_municipio_prestador>
    <tributa_municipio_tomador>N</tributa_municipio_tomador>  <!-- required by spec -->
    <codigo_local_prestacao_servico>8083</codigo_local_prestacao_servico>
    <unidade_codigo>1</unidade_codigo>
    <unidade_quantidade>1</unidade_quantidade>
    <unidade_valor_unitario>1500,00</unidade_valor_unitario>
    <codigo_item_lista_servico>1401</codigo_item_lista_servico>  <!-- LC 116 item, e.g. 14.01 for car wash -->
    <codigo_nbs>123456789</codigo_nbs>   <!-- mandatory for Tax Reform / IBS/CBS -->
    <descritivo>Lavagem completa</descritivo>
    <aliquota_item_lista_servico>5,0000</aliquota_item_lista_servico>
    <situacao_tributaria>0000</situacao_tributaria>   <!-- ⚠ verify with IPM for LC 116 item 14.01 -->
    <valor_tributavel>1500,00</valor_tributavel>
  </lista>
</itens>
```

#### `<IBSCBS>` — Tax Reform group (at `<nfse>` level)

```xml
<IBSCBS>
  <finNFSe>0</finNFSe>
  <indFinal>1</indFinal>
  <cIndOp>030102</cIndOp>
  <valores>
    <trib>
      <gIBSCBS>
        <CST>000</CST>           <!-- 3 digits — IBS/CBS Tax Situation Code (NT 004 table) -->
        <cClassTrib>000001</cClassTrib>   <!-- 6 digits — Tax Classification Code -->
      </gIBSCBS>
    </trib>
  </valores>
</IBSCBS>
```

Note: `<IBSCBS>` inside `<nf>` (rate/calculation values) is auto-calculated by IPM — do not fill manually.

#### `<forma_pagamento>`

```xml
<forma_pagamento>
  <tipo_pagamento>1</tipo_pagamento>  <!-- 1 = à vista -->
</forma_pagamento>
```

### 2.5 Digital Signature

Required by some municipalities:

- Standard: W3C XMLDSig, enveloped signature inside the XML document
- Signature algorithm: `http://www.w3.org/2000/09/xmldsig#rsa-sha1`
- Canonicalization: `http://www.w3.org/TR/2001/REC-xml-c14n-20010315`
- Certificate: X.509 from PFX (PKCS#12)

### 2.6 Response Format

Root element: `<retorno>`. Encoding: **ISO-8859-1** — always read the response stream via `XDocument.Load(stream)` to respect the XML declaration; `ReadAsStringAsync()` will corrupt Portuguese characters.

Observed real response structure (Concórdia/SC, test mode):

```xml
<?xml version="1.0" encoding="ISO-8859-1"?>
<retorno>
  <mensagem>
    <codigo>NFS-e válida para emissão.</codigo>
  </mensagem>
  <numero_nfse>4</numero_nfse>
  <serie_nfse>1</serie_nfse>
  <data_nfse>12/05/2026</data_nfse>
  <hora_nfse>09:59:02</hora_nfse>
  <situacao_codigo_nfse>1</situacao_codigo_nfse>          <!-- 1 = Emitida -->
  <situacao_descricao_nfse>Emitida</situacao_descricao_nfse>
  <link_nfse>https://concordia.atende.net/.../identificador/...</link_nfse>
  <cod_verificador_autenticidade>8083120526095902...</cod_verificador_autenticidade>
</retorno>
```

Key differences from initially documented format:
- **No `<sucesso>` element** — success is determined by `<situacao_codigo_nfse>` == `"1"`, or fallback to `<numero_nfse>` being non-empty. `<sucesso>` may appear in some portal versions; honour it if present.
- **`<mensagem>` is a wrapper** — the text is in `<mensagem><codigo>`, not directly in `<mensagem>`.
- **`<link_nfse>` not `<link_pdf>`** — the PDF/detail link uses `link_nfse` in Concórdia; check both.

- HTTP status may be `< 500` even on business errors — always parse the XML body
- In test mode (`<nfse_teste>1</nfse_teste>`), IPM validates and returns a test NFS-e number without registering a real invoice.

### 2.7 XML Escaping

| Character | Replace with |
|---|---|
| `&` | `&amp;` |
| `<` | `&lt;` |
| `>` | `&gt;` |
| `'` | `&apos;` |
| `"` | `&quot;` |
| `/` | Not allowed per IPM |

### 2.8 TOM Code Reference

TOM (Tabela de Órgãos e Municípios) codes:

- Concórdia/SC → `8083`
- Must be provided for both `<prestador/cidade>` and `<tomador/cidade>` when available

### 2.9 Known Bugs in Current `IpmXmlBuilder` Implementation

See `TASKS.md` for fix tracking.

| ID | Field / section | Issue |
|---|---|---|
| IPM-1 | `<pis_cofins>` + `<valor_pis>`/`<valor_cofins>` | Both filled simultaneously — double-count. For B2C (não retido): fill `<pis_cofins>` only |
| IPM-2 | `<cst>` inside `<pis_cofins>` | Returns `"1"` instead of `"01"` — must be 2 digits |
| IPM-3 | `<data_fato_gerador>` | Uses `DateTime.Today` (server date) instead of `invoice.IssuedAt` |
| IPM-4 | `<tipo_retencao>` | Deprecated by IPM — remove from output |
| IPM-5 | `<tributa_municipio_tomador>` | Required by spec but never emitted |
| IPM-6 | `<situacao_tributaria>` | Value `"0000"` unverified for LC 116 item 14.01 (car wash) |

---

## 3. IPM Tax Reform — NTE-122/2025 (IBS/CBS)

Reference: NTE-122/2025 v1.4 (17/12/2025)

### 3.1 Test Mode

```xml
<nfse_teste>1</nfse_teste>
```

- Value `1`: validates XML and returns `NFS-e válida para emissão.` without issuing
- Value `0` or omitted: issues normally (IBS/CBS tags ignored until 01/01/2026)

### 3.2 PIS/COFINS — Próprio vs Retido

| `tipo_retencao` | Meaning | Effect on IBS/CBS base |
|---|---|---|
| 1 | PIS/COFINS Retido | Deducted from IBS/CBS calculation base |
| 2 | PIS/COFINS Não Retido | No deduction |
| 3 | PIS Retido / COFINS Não Retido | PIS deducted only |
| 4 | PIS Não Retido / COFINS Retido | COFINS deducted only |

**Próprio** (fill `<pis_cofins>` group): PIS/COFINS is borne by the service provider — deducted from IBS/CBS base.  
**Retido** (fill `<valor_pis>`/`<valor_cofins>` outside `<pis_cofins>`): withheld by the service taker — not deducted.  
These are mutually exclusive paths. For B2C PassouLavou: use **próprio** (`tipo_retencao = 2`, fill `<pis_cofins>` only).

Note: `<tipo_retencao>` is deprecated by IPM — the portal now infers retention from the presence of `<valor_pis>`/`<valor_cofins>`. The field is still valid for the Nacional XML (`<tpRetPisCofins>`).

### 3.3 IBS/CBS CST vs PIS/COFINS CST

| Field | Tag | Digits | Tax system | Source |
|---|---|---|---|---|
| PIS/COFINS CST | `<cst>` inside `<pis_cofins>` | 2 (zero-padded) | IN RFB (federal, pre-reform) | `pisCofinsCst` |
| IBS/CBS CST | `<CST>` inside `<gIBSCBS>` | 3 | LC 214/2024 + NT 004 table | `ibsCbsCst` |

These are completely different tax systems sharing the "CST" acronym.

### 3.4 Validation Error Codes (IBS/CBS)

| Code | Description |
|---|---|
| 00336 | IBS/CBS rates in `nf/IBSCBS` require `nfse/IBSCBS` to also be filled |
| 00337 | `nfse/IBSCBS` requires rates in `nf/IBSCBS` |
| 00338 | Calculation base (vBC) incorrect |
| 00366 | NBS (Brazilian Service Nomenclature) is mandatory |
| 00368 | Link between Service List × NBS × Operation Indicator × Tax Classification not found |

### 3.5 Timeline

| Date | Requirement |
|---|---|
| Until 31/12/2025 | Test phase only (`<nfse_teste>1`) |
| From 01/01/2026 | IBS/CBS tags optional but recommended |
| Future | IBS/CBS mandatory |

---

## 4. Nacional Portal (DPS v1.01)

Reference spec: `anexo_i-sefin_adn-dps_nfse-snnfse-v1-01-20260101.xlsx`

Key field rules validated against spec:

| Field | Rule |
|---|---|
| `dCompet` | `YYYY-MM-DD` (full date, not month-only) |
| `pAliq` | Emitter-provided only when municipality is NOT on the national system AND `tribISSQN = 1` (taxable) |
| `tpRetISSQN` | `"1"` (Não Retido) — correct for B2C |
| `tpRetPisCofins` | `"2"` (Não Retido) — correct for B2C |
| PIS/COFINS CST (`cst`) | 2-char zero-padded: `"01"`–`"09"` |
| IBS/CBS CST (`CST`) | 3 digits (000–999); value correctness per NT 004 is not validated (table changes with legislation) |
| `cClassTrib` | 6 digits |
| IBSCBS group | Optional until 2027 for Simples Nacional |
| `indFinal` | Deprecated in 2026 per NT 005 |

### 4.1 Fiscal Code Validation Responsibility

| Code | Validated by microservice | Validated by accounting manager |
|---|---|---|
| PIS/COFINS CST format (2 digits, range 1–9) | ✅ | — |
| PIS/COFINS CST semantic correctness | — | ✅ |
| IBS/CBS CST format (3 digits, 000–999) | ✅ | — |
| IBS/CBS CST semantic correctness (NT 004 table) | — | ✅ |
| `cClassTrib` format (6 digits) | ✅ | — |
