# Invoice Microservice — Claude Instructions

## Source of truth

All design decisions, specs, and tasks for this repo live in `docs/`:

| File | Use for |
|---|---|
| `docs/SPECS.md` | External contracts: portal protocols, XML schemas, tax rules |
| `docs/DESIGN.md` | Architecture, auth design, coding conventions, current state |
| `docs/TASKS.md` | Backlog and completed work |

`docs/passoulavou-invoice-integration-guide.md` is a **downstream** document — it is updated to reflect decisions already captured in the trio above, never the other way around.

When uncertain about how something works or what is planned, read `docs/` first.

---

## Architecture (summary)

Layering: `Api` → `Application` → `Domain` ← `Infrastructure`

Emission runtime path:
`InvoicesController` → `EmitInvoiceCommandHandler` → `InvoiceEmissionJob` → `InvoiceEmissionWorker` → `InvoiceXmlBuilderFactory` → (`IpmXmlBuilder` | `NationalXmlBuilder`) → portal client

Portal selection is credential-driven (`PortalCredentials.PortalType`), not endpoint-driven.

---

## Coding rules

- **No MediatR, no AutoMapper.** Direct DI from controllers to handlers; explicit mapping in builders/handlers.
- Register services in `Api/Extensions/DependencyInjection.cs`, bind config in `Api/Extensions/*Config.cs`.
- Command + handler co-located in the same file (e.g. `Commands/EmitInvoice/EmitInvoiceCommand.cs`).
- Validation in FluentValidation validators, not in controllers.
- Repository reads: `AsNoTracking()`. Repository writes: `SaveChangesAsync()` immediately.
- Value objects (`Cnpj`, `Cpf`) used at entity/repository boundary; DB mapping converts to strings.

---

## Current state notes

- `api_clients` migration (`AddApiClientsTable`) has been generated but not yet applied to the target database — run `dotnet ef database update` before testing auth.
- No test projects exist yet — adding unit tests is the next priority before further feature work.
- Portal credential passwords are stored as plaintext — do not assume hashing is in place.

---

## Useful commands

```bash
dotnet build InvoiceMicroservice.sln
dotnet run --project src/InvoiceMicroservice.Api
dotnet test InvoiceMicroservice.sln
dotnet ef database update --project src/InvoiceMicroservice.Infrastructure --startup-project src/InvoiceMicroservice.Api
dotnet ef migrations add <Name> --project src/InvoiceMicroservice.Infrastructure --startup-project src/InvoiceMicroservice.Api
```
