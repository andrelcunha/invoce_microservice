# Invoice Microservice — Design

Architecture decisions, layer conventions, data flow, and patterns. The "how" of the system. For what the system must do, see `SPECS.md`.

---

## 1. Architecture Overview

```
Client (e.g. passoulavou-api)
  └─ POST /api/invoices  [X-Api-Key authenticated]
       └─ InvoicesController
            └─ EmitInvoiceCommandHandler  →  enqueues InvoiceEmissionJob
                 └─ InvoiceEmissionWorker (background)
                      └─ InvoiceXmlBuilderFactory
                           ├─ IpmXmlBuilder    →  IpmApiClient    →  IPM portal
                           └─ NationalXmlBuilder → NationalApiClient → Nacional portal
                                └─ persists InvoiceEmissionResult
Client polls GET /api/invoices/{jobId}   ←  current result delivery
                                         ←  webhook (INV-2, planned)
```

### Layer boundaries

| Project | Responsibility |
|---|---|
| `InvoiceMicroservice.Api` | Controllers, auth handlers, background worker, DI wiring, configuration extensions |
| `InvoiceMicroservice.Application` | Commands, validators, command handlers |
| `InvoiceMicroservice.Domain` | Entities, value objects (`Cnpj`, `Cpf`), repository interfaces |
| `InvoiceMicroservice.Infrastructure` | EF Core DbContext, repository implementations, XML builders, portal API clients |

Dependency flow: `Api` → `Application` → `Domain` ← `Infrastructure`

---

## 2. Authentication

### Current: Per-client API key (INV-1, implemented)

- Header: `X-Api-Key`
- `ApiKeyAuthenticationHandler` computes SHA-256 hash of the incoming key and looks it up in `api_clients` table
- On match: sets `ClaimTypes.Name` and `ClaimTypes.NameIdentifier` to `client_id`
- `InvoicesController` asserts `command.ClientId == User.FindFirstValue(ClaimTypes.NameIdentifier)` — prevents impersonation
- Fallback policy: all endpoints require authenticated user (via `AddAuthorizationBuilder().SetFallbackPolicy(...)`)

### Admin endpoint

- `POST /api/clients` — registers a new client, returns plaintext API key (`plv_<32-byte-hex>`) and webhook secret (`whsec_<32-byte-hex>`) **once**
- Protected by `X-Admin-Key` header (constant-time comparison against `Authentication:AdminKey:Key` config)
- `[AllowAnonymous]` from the main scheme — admin key is validated manually in the controller
- API key is stored as SHA-256 hash; webhook secret is stored as plaintext (needed for HMAC signing in INV-2)

### Configuration keys

```json
"Authentication": {
  "ApiKey": { "HeaderName": "X-Api-Key" },
  "AdminKey": { "Key": "<strong-random-value>" }
}
```

---

## 3. Data Model

### Key tables

| Table | Entity | Purpose |
|---|---|---|
| `api_clients` | `ApiClient` | Per-client API key hash, webhook URL, webhook secret |
| `issuers` | `IssuerEntity` | Companies that emit invoices (identified by CNPJ) |
| `portal_credentials` | `PortalCredentialsEntity` | A1 certificate + portal type per issuer |
| `invoice_emission_jobs` | `InvoiceEmissionJob` | One row per emission request; status: `Pending`, `Processing`, `Succeeded`, `FailedRetryable`, `FailedPermanent` |
| `invoice_emission_results` | `InvoiceEmissionResult` | Provider response after job settles (1:1 with job) |
| `service_type_tax_mappings` | `ServiceTypeTaxMapping` | Maps `serviceTypeKey` → CNAE, NBS, LC 116 code, tax codes |
| `municipalities` | `Municipality` | IBGE ↔ TOM code lookup table |

### Payload persistence

The full `EmitInvoiceCommand` (consumer data, tax fields, service description, amounts) is serialized as JSON into `InvoiceEmissionJob.PayloadJson` at enqueue time. The worker deserializes it at processing time and maps it to `InvoiceXmlPayload` before passing it to the XML builder. Individual invoice fields are **not** stored as separate columns on the job row.

Issuer address is stored as JSONB in `IssuerEntity.AddressJson` and deserialized by the worker — no separate address table.

### Builder selection

`InvoiceXmlBuilderFactory` reads the issuer's `PortalCredentials.PortalType` and returns either `IpmXmlBuilder` or `NationalXmlBuilder`. Selection is credential-driven, not endpoint-driven.

Note: the factory calls `issuerRepository.GetByCnpjAsync` internally, so the worker performs two issuer DB lookups per job (worker + factory). The factory also requires the `PortalCredentials` navigation property to be eager-loaded by the issuer repository.

### Tax rate ownership

Tax rates are the caller's responsibility — the microservice does not know the correct rates for a given issuer because they depend on the issuer's city, CNAE, regime tributário, and whether retention follows the prestador's or tomador's municipality. The caller (e.g. `passoulavou-api`) computes and sends these values per request:

- `issRate` — ISS rate
- `aliquotaPis`, `aliquotaCofins` — PIS/COFINS rates
- `pisCofinsCts`, `tipoRetencaoPisCofins` — PIS/COFINS CST and retention type
- `ibsCbsCst`, `ibsCbsClassTrib` — IBS/CBS tax situation and classification codes

**Exception — IBS/CBS calculation rates:** `PIbsUf`, `PIbsMun`, `PCbs`, `PRedAliqUf`, `PRedAliqMun`, `PRedAliqCbs` remain global in `TaxConfig` (bound from `appsettings.*.json`, section `TaxConfig`). This is intentional: IBS/CBS enforcement is phased in and rates are currently zero/placeholder. These will be moved to per-request when the reform becomes mandatory and per-municipality rates are published (planned ~2030). See `TASKS.md` [INV-6].

`TaxConfig.PAliquotaPis` and `TaxConfig.PAliquotaCofins` are dead fields — the builder was updated to use `invoice.AliquotaPis`/`invoice.AliquotaCofins` but the TaxConfig class was not cleaned up. They can be removed.

---

## 4. Request / Job Lifecycle

1. `POST /api/invoices` — validates payload (FluentValidation), asserts CNPJ exists and issuer is active, persists `InvoiceEmissionJob` with status `Pending`, returns `202 Accepted` with `jobId`
2. `InvoiceEmissionWorker` polls every 2s (batch size 10), atomically claims jobs via `FOR UPDATE SKIP LOCKED`, marks `Processing`
3. Worker deserializes `PayloadJson` → `EmitInvoiceJobPayload`, maps to `InvoiceXmlPayload`, selects builder, builds XML, submits to portal
4. On success: upserts `InvoiceEmissionResult`, marks job `Succeeded`
5. On failure: upserts `InvoiceEmissionResult` with error, then:
   - `attempts >= maxAttempts (5)` → `FailedPermanent` (no further retries)
   - otherwise → `FailedRetryable` with `nextRetryAt = now + 2^attempts × 15s` (exponential backoff)
   - `ClaimPendingAsync` picks up `FailedRetryable` jobs when `nextRetryAt <= now`
6. `GET /api/invoices/{jobId}` — returns current job status + result when available

---

## 5. Coding Conventions

### DI and service registration

- Register repositories and handlers in `Api/Extensions/DependencyInjection.cs`
- Bind configuration sections in `Api/Extensions/*Config.cs` files
- Use `AddScoped` for repositories and command handlers
- Do **not** introduce `MediatR` or `AutoMapper` — use direct DI from controllers to handlers and explicit mapping in builders/handlers

### Command + handler co-location

Command record and its handler class live in the same file (e.g. `Commands/EmitInvoice/EmitInvoiceCommand.cs`). Follow this pattern unless a refactor explicitly changes structure.

### Validation

- All request validation in FluentValidation validators, not in controllers
- Value objects (`Cnpj`, `Cpf`) used when touching entities/repositories
- DB mapping converts value objects to strings in `InvoiceDbContext`

### Repository pattern

- Read operations: `AsNoTracking()`
- Write/update operations: call `SaveChangesAsync()` immediately, not deferred

### Portal configuration

`PortalConfig` sections (`Ipm`, `Nacional`) in `appsettings.*.json`, bound in `Extensions/ApiClientConfig.cs`.

---

## 6. Planned: Webhook Delivery (INV-2)

After a job settles, the worker will:
1. Look up `webhook_url` + `webhook_secret` from `api_clients` by `clientId`
2. `POST {webhookUrl}` with HMAC-SHA256 signature in `X-Webhook-Signature` header
3. Retry with exponential backoff (3–5 attempts) on delivery failure

Webhook secret is stored as plaintext in `api_clients` because the microservice needs it to compute the HMAC for outbound signing. The receiver (e.g. `passoulavou-api`) holds a copy to verify incoming webhooks.

After INV-2 + NJS-1 ship, polling becomes a safety net only (cron picks up jobs with no push within ~15 min).

---

## 7. Developer Workflows

```bash
# Build
dotnet build InvoiceMicroservice.sln

# Run API
dotnet run --project src/InvoiceMicroservice.Api

# Run tests (once test projects exist)
dotnet test InvoiceMicroservice.sln

# Local Postgres
docker compose -f postgres/docker-compose.yml up -d --build

# Apply migrations
dotnet ef database update --project src/InvoiceMicroservice.Infrastructure --startup-project src/InvoiceMicroservice.Api

# Add a migration
dotnet ef migrations add <MigrationName> --project src/InvoiceMicroservice.Infrastructure --startup-project src/InvoiceMicroservice.Api
```

---

## 8. Nacional Portal Transport

`NationalApiClient` sends invoice XML as:

- Body: `{ "dpsXmlGZipB64": "<base64-encoded gzip of XML>" }`
- Auth: mutual TLS — A1 certificate (PFX) loaded per request via `X509Certificate2`
- The `Protocol` property on `NacionalPortalConfig` is always `null` in current configuration; certificate path and password are the only required fields

**Test mode:** When `NationalApiClient` is in test/debug mode (controlled by a flag in `NacionalPortalConfig`), it writes the raw XML to disk under `national-xml-output/` before sending. These files accumulate in the working directory — be aware in production environments.

**Unimplemented methods:** `QueryInvoiceAsync` and `CancelInvoiceAsync` on `NationalApiClient` both throw `NotImplementedException`. Do not call them until implemented.

**`HttpClient` instantiation:** `NationalApiClient` creates a new `HttpClient` per job call (anti-pattern — socket exhaustion risk under load). This should be refactored to use `IHttpClientFactory` or a singleton with certificate handler.

---

## 9. Known Current-State Gotchas

- **No test projects yet** — `tests/` directory and test projects do not exist. Adding unit tests is tracked as the next priority before further feature work.
- **Password "hashing"** — portal credential password storage currently stores plaintext; do not assume secure hashing is in place.
- **`api_clients` migration pending** — `AddApiClientsTable` migration was generated but `dotnet ef database update` has not been run yet against the target database.
- **`IssuerValidator` dead code** — `EmitInvoiceCommandValidator` defines an inner `IssuerValidator` class but never applies it; the CNPJ existence check is wired directly in the parent validator. The inner class is inert.
- **Silent service type fallback** — `InvoiceXmlBuilderFactory.GetServiceCodesAsync` falls back to `ServiceTypeTaxCodes.Default()` when no mapping is found for `serviceTypeKey`. No warning or error is logged; the invoice is emitted with default codes silently.
