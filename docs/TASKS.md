# Invoice Microservice — Tasks

Backlog and completed work for the `invoce_microservice` repo. The `passoulavou-api` side tasks (NJS-*) are included here for cross-reference since they depend on items in this repo.

---

## Completed

- [x] `pAliq` condition inverted in `NationalXmlBuilder.BuildValoresAsync` — fixed: `tribISSQN != 1` → `tribISSQN == 1`
- [x] PIS/COFINS CST zero-padding missing in `NationalXmlBuilder` — fixed: `.ToString()` → `.ToString("D2")`
- [x] **[INV-1]** Per-client API key auth: `api_clients` table, SHA-256 hash lookup, `clientId` claim validated against request body, `POST /api/clients` admin endpoint, EF migration generated (`AddApiClientsTable`)
- [x] **[INV-4]** Retry policy already implemented in `InvoiceEmissionWorker` + `InvoiceEmissionJobRepository`: `FailedRetryable` (exponential backoff, 2^attempts × 15s) vs `FailedPermanent` (after 5 attempts); `ClaimPendingAsync` picks up retryable jobs atomically via `FOR UPDATE SKIP LOCKED`
- [x] **[IPM-2]** `NormalizePisCofinsCst` now uses `.ToString("D2")` — CST is correctly zero-padded to 2 digits (e.g. `"01"` not `"1"`)
- [x] **[IPM-R1]** IPM response encoding — switched from `ReadAsStringAsync()` to `XDocument.Load(stream)` so ISO-8859-1 responses are decoded correctly (Portuguese chars were appearing as `�`)
- [x] **[IPM-R2]** IPM response parser — fixed `<mensagem>` text path (was `root.Element("mensagem").Value`, actual text is in `<mensagem><codigo>`); fixed success detection (no `<sucesso>` element in real responses — now uses `<situacao_codigo_nfse>=="1"` with `<sucesso>` as fallback)

---

## Backlog — `invoce_microservice`

### [INV-2] Webhook delivery on job completion — Priority: High

Depends on: INV-1 ✅

**Problem:** Result delivery is polling-only. `passoulavou-api` cron polls `GET /api/invoices/{jobId}` every tick — latency up to minutes, wasted HTTP calls.

**Decision:** Webhook URL stored in `api_clients` per client (registered once, not per-request).

**Solution:**
- After job settles (succeeded or failed), worker looks up `webhook_url` + `webhook_secret` from `api_clients` by `clientId`
- `POST {webhookUrl}` with HMAC-SHA256 signature in `X-Webhook-Signature` header
- Retry with exponential backoff (3–5 attempts) on delivery failure

---

### [INV-3] `dhEmi` timezone dependency — Priority: Low

`DateTime.Now` used for emission datetime in `NationalXmlBuilder`. Safe while server is BRT, fragile if containerized in a different timezone.

**Fix:** `TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"))`

---

### [INV-6] Move IBS/CBS calculation rates to per-request — Priority: Low (deferred ~2030)

Currently `PIbsUf`, `PIbsMun`, `PCbs`, `PRedAliqUf`, `PRedAliqMun`, `PRedAliqCbs` are global in `TaxConfig` (appsettings). IBS/CBS rates vary by issuer state and municipality; keeping them global will be wrong once multiple issuers in different cities are active and rates are enforced.

**Decision:** Intentionally deferred. IBS/CBS enforcement is phased; rates are currently placeholder/zero and not retained in production. Revisit when reform becomes mandatory and per-municipality rates are published.

**When the time comes:**
- Add these six fields to `EmitInvoiceCommand.Data` and `InvoiceXmlPayload`
- Remove their reads from `BuildIbsCbsNfSectionAsync` (replace with `invoice.*`)
- Delete `TaxConfig` class and its DI registration
- Update `passoulavou-api` to send the rates per request

---

### [INV-5] Input validation for fiscal codes — Priority: Medium

Validate at the `totem_fiscal_config` input boundary, not at emission time.

**Rules:**
- PIS/COFINS `pisCofinsCst`: numeric, 1–2 char input, range 1–9, zero-pad to `"01"`–`"09"` on output. `"23"` has valid format but is out of range — reject at config time.
- IBS/CBS `ibsCbsCst`: numeric, exactly 3 chars, range 000–999. Semantic correctness (NT 004 table) is the accounting manager's responsibility — not validated here.
- `ibsCbsClassTrib`: numeric, exactly 6 chars.

---

### [IPM-1] PIS/COFINS double-counting in `IpmXmlBuilder` — Priority: Critical

`BuildPisCofinsSection` fills `<pis_cofins>` (próprio) AND `BuildNfSectionAsync` emits `<valor_pis>`/`<valor_cofins>` (retido) simultaneously — double-count bug.

**Fix:** For B2C PassouLavou (não retido): emit `<pis_cofins>` group only; leave `<valor_pis>`/`<valor_cofins>` empty.

File: `src/InvoiceMicroservice.Infrastructure/Xml/IpmXmlBuilder.cs`

---

### [IPM-2] `<cst>` zero-padding missing in `IpmXmlBuilder` — Priority: High

`NormalizePisCofinsCst` returns `"1"` instead of `"01"`. IPM spec requires exactly 2 digits.

**Fix:** `.ToString("D2", CultureInfo.InvariantCulture)`

File: `src/InvoiceMicroservice.Infrastructure/Xml/IpmXmlBuilder.cs`

---

### [IPM-3] `data_fato_gerador` uses server date — Priority: High

Uses `DateTime.Today` (server date at processing time) instead of the actual service date from the invoice.

**Fix:** `invoice.IssuedAt.ToString("dd/MM/yyyy")`

File: `src/InvoiceMicroservice.Infrastructure/Xml/IpmXmlBuilder.cs`

---

### [IPM-4] `<tipo_retencao>` deprecated — Priority: Medium

IPM discontinued this field. Portal infers PIS/COFINS retention from presence of `<valor_pis>`/`<valor_cofins>`. Emitting the tag is currently harmless but may cause errors in future IPM versions.

**Fix:** Remove `<tipo_retencao>` from XML output.

File: `src/InvoiceMicroservice.Infrastructure/Xml/IpmXmlBuilder.cs`

---

### [IPM-5] `<tributa_municipio_tomador>` never emitted — Priority: Medium

Required by IPM spec but missing from the builder output.

File: `src/InvoiceMicroservice.Infrastructure/Xml/IpmXmlBuilder.cs`

---

### [IPM-6] `<situacao_tributaria>` value unverified — Priority: Low

Current value `"0000"` for LC 116 item 14.01 (car wash). Needs confirmation with IPM documentation or support.

**Action:** Verify correct `situacao_tributaria` code for car-wash service type (LC 116 item 14.01) with IPM before going live.

File: `src/InvoiceMicroservice.Infrastructure/Xml/IpmXmlBuilder.cs`

---

### [NAC-1] `HttpClient` created per job in `NationalApiClient` — Priority: Medium

Each call to `NationalApiClient` instantiates a new `HttpClient` with a per-request `SocketsHttpHandler` carrying the A1 certificate. Under any meaningful load this will exhaust ephemeral ports.

**Fix:** Refactor to `IHttpClientFactory` with a named client configured per issuer, or use a singleton `HttpClient` with the certificate handler injected at startup.

File: `src/InvoiceMicroservice.Infrastructure/ApiClients/NationalApiClient.cs`

---

### [NAC-2] `QueryInvoiceAsync` / `CancelInvoiceAsync` not implemented — Priority: Medium

Both methods on `NationalApiClient` throw `NotImplementedException`. Do not expose or call them until implemented.

File: `src/InvoiceMicroservice.Infrastructure/ApiClients/NationalApiClient.cs`

---

### [CLEAN-3] Remove dead `PAliquotaPis`/`PAliquotaCofins` from `TaxConfig` — Priority: Low

`TaxConfig` still declares `PAliquotaPis` and `PAliquotaCofins` but `IpmXmlBuilder` was updated to read from `invoice.AliquotaPis`/`invoice.AliquotaCofins` instead. The fields are unused.

**Fix:** Remove the two properties from `TaxConfig`. No other code references them.

File: `src/InvoiceMicroservice.Infrastructure/Xml/TaxConfig.cs`

---

### [CLEAN-1] `IssuerValidator` dead code in `EmitInvoiceCommandValidator` — Priority: Low

An inner `IssuerValidator` class is defined but never applied. The CNPJ existence check is wired directly in the parent validator. The inner class is unreachable.

**Fix:** Delete the unused inner class.

File: `src/InvoiceMicroservice.Application/Commands/EmitInvoice/EmitInvoiceCommand.cs`

---

### [CLEAN-2] Silent fallback on missing service type mapping — Priority: Low

`InvoiceXmlBuilderFactory.GetServiceCodesAsync` silently falls back to `ServiceTypeTaxCodes.Default()` when `serviceTypeKey` has no row in `service_type_tax_mappings`. No warning is logged; invoices are emitted with default codes without any signal to operators.

**Fix:** Log a warning (or throw) when the fallback is triggered so misconfigured service types are visible in logs.

File: `src/InvoiceMicroservice.Infrastructure/Xml/InvoiceXmlBuilderFactory.cs`

---

## Cross-reference — `passoulavou-api` (NestJS)

These are tracked here because they depend on items above.

### [NJS-1] Webhook receiver `POST /invoices/webhook` — Priority: High

Depends on: INV-2

No endpoint exists to receive push callbacks from the microservice.

**Solution:**
- `POST /invoices/webhook`
- Validate HMAC-SHA256 signature from `X-Webhook-Signature` against `INVOICE_WEBHOOK_SECRET` env var
- Call `syncEmissionStatusByJobId` with push payload (avoids an extra GET round-trip)
- Return `204 No Content`

---

### [NJS-2] Demote polling cron to safety net — Priority: Medium

Depends on: NJS-1

After webhook is live, `processPendingEmissions` cron becomes redundant as primary path.

**Fix:** Add minimum age check — cron only picks up jobs still in `queued`/`processing` after N minutes (e.g. 15 min) without a status update.

---

### [NJS-3] Race condition in `startEmissionForUsedWash` — Priority: Medium

Non-atomic check-then-insert. Two concurrent calls for the same `usedWashId` can both pass the `findByUsedWashId` guard and insert duplicate rows.

**Fix:** Unique DB constraint on `invoice_emission.used_wash_id`. Catch the unique-violation error and return the existing row.

---

### [NJS-4] Failed emissions not retried — Priority: Low

`processPendingEmissions` cron excludes `failed` rows. The microservice retry contract is already defined (INV-4 ✅): `FailedRetryable` jobs are re-queued automatically on the microservice side. On the NestJS side, align the cron to treat a job returning to `queued`/`processing` status as a normal in-progress state rather than surfacing it as an error.
