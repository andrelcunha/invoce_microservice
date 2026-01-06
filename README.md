```markdown
# Invoice Microservice (.NET)

## Purpose

This microservice acts as a secure, compliant gateway for emitting Brazilian electronic service invoices (NFS-e) through IPM's **Emissor Nacional** API.

It receives invoice emission requests from internal services, performs thorough validation (CPF/CNPJ, business rules, Brazilian tax requirements), generates the required IPM-compliant XML, submits it to the external API, handles responses/retries, and stores everything needed for legal auditing.

Key features:
- Hybrid interface: Synchronous HTTP API for submission (immediate validation feedback) + asynchronous background processing.
- Persistence in PostgreSQL for compliance and retry support.
- Strong focus on Brazilian tax rules (CNPJ/CPF validation, IBGE municipality codes, CNAE mapping, etc.).
- Built with modern .NET 8 best practices (Clean Architecture lite, FluentValidation, EF Core, Polly).

The service is currently under active development as part of the migration from the original Node.js implementation.

## Project Structure

```
InvoiceMicroservice.sln
├── src/
│   ├── InvoiceMicroservice.Api/              # Entry point: Minimal APIs / Controllers, Program.cs, configuration
│   ├── InvoiceMicroservice.Application/      # Commands, validators, services, handlers
│   ├── InvoiceMicroservice.Domain/           # Entities, value objects (Cnpj, Cpf), enums
│   ├── InvoiceMicroservice.Infrastructure/   # EF Core DbContext, repositories, external clients (IPM), XML builder
│   └── InvoiceMicroservice.Shared/           # Shared DTOs, extensions (optional)
└── tests/
    ├── InvoiceMicroservice.UnitTests/
    └── InvoiceMicroservice.IntegrationTests/
```

## How to Run

### Prerequisites
- .NET 10.0 SDK
- PostgreSQL (running instance – the service uses an existing shared database)
- (Optional) RabbitMQ if enabling outgoing events later

### Setup
1. Clone the repository and open the solution:

   ```bash
   git clone <repo-url>
   cd InvoiceMicroservice
   code .  # or open InvoiceMicroservice.sln in your IDE
   ```

2. Configure the PostgreSQL connection string:

   - Copy `src/InvoiceMicroservice.Api/appsettings.json.example` to `appsettings.json` (if exists) or add directly:
   ```json
   {
     "ConnectionStrings": {
       "Postgres": "Host=your-host;Database=your-db;Username=your-user;Password=your-pass"
     }
   }
   ```
   - Or set via environment variable / user secrets.

3. Apply database migrations:

   ```bash
   dotnet ef database update --project src/InvoiceMicroservice.Infrastructure --startup-project src/InvoiceMicroservice.Api
   ```

### Run the service

```bash
dotnet run --project src/InvoiceMicroservice.Api
```

The API will start on `https://localhost:7xxx` (port varies) with Swagger available at `/swagger`.

### Quick test

- Open Swagger UI
- Use `POST /api/invoices` to submit a test payload (validation will respond immediately)

---