# Invoice Microservice - .NET Migration Guide (Updated)

## 📋 Executive Summary

This updated document reflects the consolidated decisions made during the migration planning from Node.js to .NET. The service remains an **asynchronous gateway** between internal microservices and IPM's "Emissor Nacional" API, abstracting XML composition complexity.

Key changes from the original specification:

- **Hybrid interface**: Primary synchronous HTTP API entrypoint for immediate validation feedback, with internal asynchronous processing (queue + background worker).
- **Persistence**: Added lightweight Postgres database for compliance, auditing, and retry support.
- **Technology stack**: Modern .NET best practices (ASP.NET Core 8+, EF Core with Npgsql, FluentValidation, MassTransit optional, Polly for resilience).
- **Flexibility**: RabbitMQ kept as optional internal queue / outgoing events (can be enabled for full decoupling if needed later).

### Updated Architectural Principles

- **Single External Integration**: All invoices route through IPM's Emissor Nacional API.
- **Multi-Tenant by CNPJ**: Multiple internal services emit invoices using different company CNPJs.
- **Hybrid Synchronous/Asynchronous Processing**: Synchronous validation via HTTP, asynchronous submission to IPM with retry logic.
- **XML Abstraction Layer**: Encapsulates IPM's XML format requirements.
- **Compliance-Driven Persistence**: Store invoice metadata and XML for legal archiving (Brazilian NFS-e requirements).

## 🎯 Core Business Purpose

The microservice **receives invoice emission requests** via HTTP API, **validates them synchronously**, persists the request, enqueues for asynchronous processing, transforms to IPM-compliant XML, submits to the API, and provides status via polling or optional events.

### Responsibilities

1. **Receive** standardized invoice requests via HTTP POST.
2. **Validate** business rules synchronously (CPF/CNPJ, amounts, UF, email, etc.) and return immediate errors if invalid.
3. **Persist** request metadata for audit/compliance.
4. **Enqueue** valid requests for background processing.
5. **Transform** to IPM XML format.
6. **Submit** to IPM Emissor Nacional API with retries.
7. **Update** persisted status and store response/XML.
8. **Expose** status via HTTP polling endpoint.
9. **Optionally publish** status events to RabbitMQ for legacy/full-async consumers.

## 🔌 Interfaces & Integrations

### 1. Primary Interface: HTTP API (Synchronous Entry)

**Base Path**: `/api/invoices`

#### POST /api/invoices

- **Purpose**: Submit new invoice emission request.
- **Request Format**: JSON (same structure as original RabbitMQ command).
- **Validation**: Synchronous using FluentValidation.
- **Success Response**:
  - `202 Accepted` with `Location: /api/invoices/{id}` and body containing the new invoice ID.
- **Error Response**:
  - `400 Bad Request` with detailed validation errors (ProblemDetails format).
- **Behavior**: On success → persist with status `Pending`, enqueue for background processing.

**Request Example** (same as original command JSON):

```json
{
  "clientId": "accounting-api",
  "data": {
    "issuer": { /* ... same as original ... */ },
    "consumer": { /* ... same as original ... */ },
    "serviceDescription": "Lavagem completa + enceramento",
    "amount": 175.50,
    "issueDate": "2025-01-15T14:30:00Z"
  }
}
```

#### GET /api/invoices/{id}

- **Purpose**: Poll invoice status and details.
- **Response**: Full invoice status, including `status`, `externalInvoiceId`, `externalInvoiceUrl`, base64 XML (if emitted), error details (if failed).

### 2. Optional: RabbitMQ Integration (for Events or Legacy)

- **Incoming**: Optional – can add a RabbitMQ consumer later for direct queue submission (bypassing HTTP validation).
- **Outgoing Events Exchange** (kept for compatibility):
  - Exchange: `invoice-exchange` (topic)
  - Routing Key: `invoice.emitted.{clientId}`
  - Message: Same JSON event structure as original (with `pending`, `emitted`, `failed` statuses).
  - Published on status changes (useful if clients prefer push notifications).

### 3. IPM Emissor Nacional API

Unchanged from original:
- Single endpoint.
- XML request/response formats preserved.
- Authentication per CNPJ (certificates or API keys, stored securely).

## 🛠️ Updated .NET Implementation Recommendations

### Technology Stack

- **Framework**: ASP.NET Core 8.0+
- **API Style**: Minimal APIs or Controllers (with FluentValidation integration)
- **Database**: Entity Framework Core + Npgsql (Postgres provider)
- **Validation**: FluentValidation (in Application/Domain layer)
- **Background Processing**: 
  - Primary: `IHostedService` + `System.Threading.Channels` (in-memory queue) or Hangfire
  - Optional: MassTransit with RabbitMQ for internal queue and outgoing events
- **HTTP Resilience**: Polly + IHttpClientFactory for IPM calls
- **XML Processing**: `System.Xml.Linq` (XDocument/XElement)
- **Logging/Monitoring**: Serilog + structured logging
- **Secrets**: Azure Key Vault / User Secrets / Environment variables
- **Testing**: xUnit + FluentAssertions + Moq

### Recommended Project Structure (Clean Architecture Lite)

```
InvoiceMicroservice.sln
├── src/
│   ├── InvoiceMicroservice.Api/              # Entry point (Minimal APIs/Controllers, Program.cs)
│   ├── InvoiceMicroservice.Application/      # Commands, Handlers, Validators, Services
│   ├── InvoiceMicroservice.Domain/           # Entities, Value Objects (Cnpj, Cpf), Enums
│   ├── InvoiceMicroservice.Infrastructure/   # EF Core, Repositories, IPM Client, XML Builder, Queue Producers
│   └── InvoiceMicroservice.Shared/           # DTOs, Extensions
└── tests/
    ├── InvoiceMicroservice.UnitTests/
    └── InvoiceMicroservice.IntegrationTests/
```

### Data Persistence (Postgres)

**Table**: `invoices` (lower_case convention optional)

| Column              | Type                  | Description                          | Indexed |
|---------------------|-----------------------|--------------------------------------|---------|
| id                  | UUID                  | Primary key                          | PK      |
| client_id           | TEXT                  | Requesting service identifier        | Yes     |
| issuer_cnpj         | TEXT(14)              | Normalized CNPJ                      | Yes     |
| issuer_data         | JSONB                 | Full issuer object                   |         |
| consumer_data       | JSONB                 | Full consumer object                 |         |
| amount              | NUMERIC(10,2)         | Invoice value                        |         |
| service_description | TEXT                  | Description                          |         |
| status              | TEXT                  | 'pending', 'emitted', 'failed'       | Yes     |
| external_invoice_id | TEXT                  | IPM protocol/number                  | Yes     |
| xml_payload         | TEXT                  | Sent XML (for audit)                 |         |
| xml_response        | TEXT                  | Received XML                         |         |
| external_response   | JSONB                 | Parsed IPM response                  |         |
| error_details       | JSONB                 | Error info if failed                 |         |
| retry_count         | INTEGER               | Retry attempts                       |         |
| created_at          | TIMESTAMPTZ           | Creation timestamp                   | Yes     |
| updated_at          | TIMESTAMPTZ           | Last update                          |         |
| issued_at           | TIMESTAMPTZ           | Emission timestamp                   |         |

**Indexes**:
- Composite: `(status, created_at)` for retry queue queries
- `(client_id)`, `(issuer_cnpj)`, `(external_invoice_id)`

### Key Implementation Patterns

- Synchronous validation on HTTP POST → immediate 400 if invalid.
- On success → persist → enqueue → return 202.
- Background worker processes queue → XML gen → IPM call (with Polly retries) → update DB → optional event publish.
- All Brazil-specific validations (CNPJ/CPF digits, UF list, IBGE code lookup, CNAE → service code mapping) in FluentValidation rules or domain value objects.

## 🔄 Migration Checklist (Updated)

### Preserved from Original

- JSON command structure (for HTTP body compatibility)
- IPM XML format and transformation logic
- Validation rules (CPF/CNPJ, amounts, UF, etc.)
- Retry policy (3 attempts, exponential backoff)
- Event JSON structure (if RabbitMQ events enabled)

### Changed / Added

- Primary interface: HTTP API instead of pure RabbitMQ
- Persistence: Required (Postgres) for compliance
- Synchronous validation feedback
- Status polling endpoint
- Optional RabbitMQ for outgoing events only
- Modern .NET stack with Clean Architecture principles

### Future-Proofing

- Easy to add pure RabbitMQ consumer later (for high-volume or legacy clients)
- Easy to add webhooks/SignalR for push notifications
- Structured logging and health checks from day one
