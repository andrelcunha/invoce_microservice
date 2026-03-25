# PassouLavou -> InvoiceMicroservice Integration Guide

This guide describes how the PassouLavou application should integrate with the invoice emission API exposed by this service, specifically the `CreateInvoice` flow in `InvoicesController`.

## Goal

PassouLavou should call this API whenever it needs to emit an NFS-e for a service already performed.

The issuer already exists in our database, so PassouLavou does not need to create or manage issuer data here. It only needs to:

1. build the JSON payload correctly;
2. call `POST /api/invoices` with the configured API key;
3. store the returned `jobId`;
4. poll `GET /api/invoices/{jobId}` until processing finishes.

## Authentication

This API is protected by an API key and expects the caller to send the shared key in the `X-Api-Key` header on every request.

- Header name: `X-Api-Key`
- Applies to: `POST /api/invoices` and `GET /api/invoices/{jobId}`
- Caller: PassouLavouAPI

Example:

```http
X-Api-Key: <shared-api-key>
```

Important:

- requests without a valid API key will be rejected with `401 Unauthorized`;
- the API key is an application credential, not an end-user credential;
- PassouLavou should load this key from secure configuration such as environment variables or a secrets manager, never hardcode it in source code.

## Endpoint Summary

### 1. Create invoice emission job

- Method: `POST`
- Path: `/api/invoices`
- Content-Type: `application/json`
- Required header: `X-Api-Key: <shared-api-key>`
- Behavior: validates the payload and enqueues an asynchronous invoice emission job

Important: this endpoint does **not** mean the invoice was issued immediately. A successful `POST` only means the request was accepted and put in the queue.

### 2. Check job status

- Method: `GET`
- Path: `/api/invoices/{jobId}`
- Required header: `X-Api-Key: <shared-api-key>`
- Behavior: returns the queue/job status and, when available, the provider result

## Request Contract

The request body maps to `EmitInvoiceCommand`.

```json
{
  "clientId": "lavacarro_api",
  "issuerCnpj": "64224814000192",
  "data": {
    "nfseSeries": 1,
    "nfseNumber": 17,
    "consumer": {
      "name": "Lorem Ipsum da Silva",
      "cpfCnpj": "12345678909",
      "email": "customer@email.com",
      "phone": "41999471672",
      "address": {
        "street": "Rua Lorem Ipsum",
        "number": "123",
        "complement": "Bloco A",
        "neighborhood": "Bairro dos Lorem",
        "city": "Loremlandia",
        "uf": "SC",
        "zipCode": "80000-000",
        "ibgeCode": "4204301",
        "tomCode": "8803"
      }
    },
    "serviceDescription": "Lavagem completa",
    "amount": 1,
    "issuedAt": "2026-02-23T15:13:15.387Z",
    "serviceTypeKey": "vehicle-wash-45200-05",
    "municipalTaxCode": "",
    "issRate": 0.035,
    "pisCofinsCts": 1,
    "aliquotaPis": 0.00,
    "aliquotaCofins": 0.0,
    "tipoRetencaoPisCofins": "0",
    "ibsCbsClassTrib": "000001",
    "ibsCbsCst": "000"
  },
  "isTestMode": true
}
```

## Field Notes

### Top-level fields

- `clientId`: required. Identifies the calling system. For PassouLavou, use a stable identifier such as `passoulavou` or `lavacarro_api`.
- `issuerCnpj`: required. Must be a valid CNPJ and must already exist as an active issuer in this system.
- `data`: required. Contains invoice data.
- `isTestMode`: optional in practice, but strongly recommended to send explicitly. `true` means homologation/test flow. `false` means production.

### `data` fields

- `nfseSeries`: required integer. Must be `> 0` and `< 10000`.
- `nfseNumber`: required integer. Must be `> 0` and `< 1000000`.
- `consumer`: required object.
- `serviceDescription`: required string, max 2000 chars.
- `amount`: required decimal. Must be `> 0` and `< 1000000`.
- `issuedAt`: required date. Cannot be in the future.
- `serviceTypeKey`: optional by validator, but should be treated as required by the integration. It is used to resolve tax/service mapping.
- `municipalTaxCode`: optional string.
- `issRate`: required decimal between `0.02` and `0.05`.
- `pisCofinsCts`: optional integer, but if sent it must be one of: `1-9`, `49-56`, `60-67`, `70-75`, `98`, `99`.
- `aliquotaPis`: decimal.
- `aliquotaCofins`: decimal.
- `tipoRetencaoPisCofins`: string.
- `ibsCbsClassTrib`: string.
- `ibsCbsCst`: string.

### `consumer` fields

- `name`: required, max 200 chars.
- `cpfCnpj`: required. Must be a valid CPF or CNPJ.
- `email`: optional, but if sent it must be a valid email.
- `phone`: optional, but if sent it must contain only digits, with 10 or 11 digits total.
- `address`: required object.

### `consumer.address` fields

- `street`: required, max 200 chars.
- `number`: required, max 20 chars.
- `complement`: optional, max 100 chars.
- `neighborhood`: required, max 100 chars.
- `city`: required, max 100 chars.
- `uf`: required, must be a valid Brazilian UF.
- `zipCode`: required, must match `NNNNN-NNN` or `NNNNNNNN`.
- `ibgeCode`: not validated by the API validator, but should always be sent because downstream XML generation uses municipality code.
- `tomCode`: not validated by the API validator, but should be sent when available because some providers use municipal/tomador city codes.

## Expected Create Response

If the payload passes validation, the API returns `202 Accepted`.

Example:

```json
{
  "jobId": "b8fc0f52-6cf1-4f51-b0fa-2d4f6b64f4f2",
  "jobStatus": "Pending"
}
```

The response also includes a `Location` header pointing to:

```text
/api/invoices/{jobId}
```

## Expected Status Response

PassouLavou must poll `GET /api/invoices/{jobId}`.

Example:

```json
{
  "jobId": "b8fc0f52-6cf1-4f51-b0fa-2d4f6b64f4f2",
  "jobStatus": "Succeeded",
  "attempts": 1,
  "maxAttempts": 5,
  "lastError": null,
  "createdAt": "2026-03-23T12:00:00Z",
  "updatedAt": "2026-03-23T12:00:08Z",
  "result": {
    "codStatus": "SUCCESS",
    "statusDescription": "NFS-e emitida com sucesso",
    "numeroDfe": "17",
    "serieDfe": "1",
    "protocolo": "123456",
    "chaveAcesso": "....",
    "verificationCode": "....",
    "documentUrl": null,
    "issuedAt": "2026-02-23T15:13:15.387Z",
    "providerProcessedAt": null,
    "errorMessage": null,
    "createdAt": "2026-03-23T12:00:08Z",
    "updatedAt": "2026-03-23T12:00:08Z"
  }
}
```

## Recommended Client Behavior

PassouLavou should implement the following flow:

1. Validate its own payload before calling this API.
2. Send `POST /api/invoices` with `X-Api-Key`.
3. If response is `202`, store `jobId` in its database.
4. Poll `GET /api/invoices/{jobId}` every few seconds, always including `X-Api-Key`.
5. Stop polling when `jobStatus` reaches a terminal state.
6. Persist returned provider fields such as `numeroDfe`, `protocolo`, `verificationCode`, `chaveAcesso`, and any error message.

Recommended polling:

- first poll after 2 to 5 seconds;
- then every 5 seconds;
- timeout after a business-defined window, for example 2 to 5 minutes;
- if timing out, keep the job as pending for later reconciliation.

## How to Interpret Job Status

Treat these statuses conceptually:

- `Pending`: job accepted but not processed yet.
- `Processing`: job claimed/being processed.
- `Succeeded`: processing finished successfully.
- `Failed`: processing failed, possibly retryable.
- terminal failure: if `jobStatus` is failed and retries are exhausted, treat the emission as failed and surface `lastError` / `result.errorMessage`.

Important: `result.codStatus` is the provider-level outcome, while `jobStatus` is the queue-processing outcome. In normal successful cases, both should indicate success.

## Validation Errors

If the request body is invalid, the API returns `400 Bad Request` with a validation problem payload.

Typical structure:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Data.IssRate": [
      "ISS rate must be between 2% (0.02) and 5% (0.05)"
    ],
    "Data.Consumer.Phone": [
      "Phone must have 10 or 11 digits (DDD + number)."
    ]
  }
}
```

PassouLavou should log the full validation response and not retry automatically until the payload is corrected.

## Business / Operational Preconditions

Before using this integration in production, these conditions must already be true in our system:

- the issuer exists in our database;
- the issuer is active;
- the issuer has address data configured;
- the issuer has portal credentials configured;
- the issuer portal type is supported;
- the service type / tax mapping exists for the sent `serviceTypeKey`, or the fallback mapping behavior is acceptable for that issuer/provider.

Important: some of these checks happen only after the job is queued. Because of that, `202 Accepted` does not guarantee final success.

## Known Integration Caveats

- There is no idempotency key in `POST /api/invoices`.
- If PassouLavou retries the same POST blindly, duplicate emission attempts may be created.
- Because of that, PassouLavou should create its own idempotency rule, for example using its internal service-order id plus issuer CNPJ plus invoice number.
- `nfseSeries` and `nfseNumber` are provided by the caller; this API does not generate them automatically.
- `issuedAt` must not be future-dated relative to server time.
- `phone` must be digits only. Do not send formatting like parentheses, spaces, or hyphens.
- `cpfCnpj` and `issuerCnpj` should be sent as digits only to avoid formatting inconsistencies, even though validation normalizes the documents.

## Suggested PassouLavou Mapping

Recommended mapping from PassouLavou domain to this API:

- `clientId`: fixed integration identifier, for example `passoulavou`
- `issuerCnpj`: issuer/company CNPJ already registered in our system
- `data.nfseSeries`: series configured for that issuer
- `data.nfseNumber`: next invoice/RPS number controlled by PassouLavou or by the agreed integration rule
- `data.consumer.*`: customer data from the service order
- `data.serviceDescription`: human-readable description of the wash service
- `data.amount`: final service amount
- `data.issuedAt`: service completion timestamp or emission timestamp, as agreed by business rules
- `data.serviceTypeKey`: fixed mapping agreed with our tax setup, for example `vehicle-wash-45200-05`
- `data.issRate`: issuer/service ISS rate
- `data.pisCofinsCts`, `data.aliquotaPis`, `data.aliquotaCofins`, `data.tipoRetencaoPisCofins`, `data.ibsCbsClassTrib`, `data.ibsCbsCst`: tax fields agreed with fiscal rules for that issuer

## Suggested Error-Handling Rules in PassouLavou

- On `400`: treat as payload error, show actionable message, do not retry automatically.
- On `401`: treat as authentication/configuration error, alert/log, and do not retry until the API key is corrected.
- On `404` during polling: treat as unknown job id and alert/log.
- On `202`: do not mark the invoice as issued yet.
- On job failure: show/store `lastError` and `result.errorMessage`.
- On network timeout calling `POST`: check whether PassouLavou can safely reconcile before retrying, to avoid duplicates.

## Suggested Implementation Checklist For The PassouLavou Agent

1. Add an HTTP client for this service with configurable base URL and timeout.
2. Configure the shared API key securely and send it in the `X-Api-Key` header for every request.
3. Create DTOs for `CreateInvoice` request and for `GetInvoiceStatus` response.
4. Add local validation before sending requests.
5. Add a service method to enqueue invoice emission.
6. Persist returned `jobId`.
7. Add polling/reconciliation logic for invoice jobs.
8. Prevent duplicate POSTs for the same business event.
9. Store the final provider identifiers returned by the status endpoint.
10. Keep `isTestMode=true` in homologation and only switch to `false` in production.

## Source References In This Repository

- `POST /api/invoices` and `GET /api/invoices/{id}`: `src/InvoiceMicroservice.Api/Controllers/InvoicesController.cs`
- request contract and enqueue behavior: `src/InvoiceMicroservice.Application/Commands/EmitInvoice/EmitInvoiceCommand.cs`
- validation rules: `src/InvoiceMicroservice.Application/Commands/EmitInvoice/EmitInvoiceCommandValidator.cs`
- asynchronous processing flow: `src/InvoiceMicroservice.Api/Background/InvoiceEmissionWorker.cs`
- provider/builder resolution by issuer configuration: `src/InvoiceMicroservice.Infrastructure/Xml/InvoiceXmlBuilderFactory.cs`

## Important Code-Based Notes

- The API route is defined in `InvoicesController` with `[Route("api/[controller]")]`, so the concrete route is `/api/invoices`.
- The API is protected by API key authentication and expects `X-Api-Key` on invoice endpoints.
- `POST /api/invoices` validates first and returns `202 Accepted` with `jobId` and `jobStatus = "Pending"` when accepted.
- The actual emission is done asynchronously by `InvoiceEmissionWorker`.
- The worker enriches the request with issuer data from our database, builds provider-specific XML, and submits it to the configured provider.
- Provider selection depends on the issuer's configured portal credentials and address.
- Authentication is configured globally in startup, so requests without a valid API key will be rejected before reaching the controller.
