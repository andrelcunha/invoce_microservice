# Invoice Microservice

Async NFS-e emission service built in C# / ASP.NET Core (.NET 10). Receives invoice jobs via REST, builds the required XML (IPM or Nacional portal, selected per issuer), submits to the government portal, and persists the result.

Used by PassouLavou and designed to support multiple internal clients.

---

## Quick Start

**Prerequisites:** .NET 10 SDK, PostgreSQL

```bash
# 1. Configure the connection string and API keys
cp src/InvoiceMicroservice.Api/appsettings.json appsettings.Development.json
# edit ConnectionStrings:Postgres, Authentication:AdminKey:Key

# 2. Apply migrations
dotnet ef database update --project src/InvoiceMicroservice.Infrastructure --startup-project src/InvoiceMicroservice.Api

# 3. Run
dotnet run --project src/InvoiceMicroservice.Api
# Swagger: https://localhost:<port>/swagger

# 4. Register a client (one-time)
curl -X POST https://localhost:<port>/api/clients \
  -H "X-Admin-Key: <your-admin-key>" \
  -H "Content-Type: application/json" \
  -d '{ "clientId": "passoulavou", "webhookUrl": "https://..." }'
# Response contains apiKey and webhookSecret — store them securely, shown once only
```

---

## Documentation

| File | Contents |
|---|---|
| [`docs/SPECS.md`](docs/SPECS.md) | External contracts: IPM protocol, XML schemas, tax rules (NTE-122), Nacional spec |
| [`docs/DESIGN.md`](docs/DESIGN.md) | Architecture, layers, auth, data model, coding conventions |
| [`docs/TASKS.md`](docs/TASKS.md) | Backlog and completed work |
| [`docs/passoulavou-invoice-integration-guide.md`](docs/passoulavou-invoice-integration-guide.md) | Integration guide for the PassouLavou client |

---

## Solution Structure

```
src/
  InvoiceMicroservice.Api/            # Controllers, auth, background worker, DI wiring
  InvoiceMicroservice.Application/    # Commands, validators, handlers
  InvoiceMicroservice.Domain/         # Entities, value objects, repository interfaces
  InvoiceMicroservice.Infrastructure/ # EF Core, repositories, XML builders, API clients
```
