# FraudDetection Sample

## What this demonstrates

- `AddCepWeb()` with the new DI-backed `ICepConfigRepository`.
- `CepWindowBuilder` for readable CEP window configuration.
- Multi-tenant CEP isolation via the `x-tenant-id` request header.
- A practical transaction endpoint that raises an alert when the same card spikes inside a short time window.

## Quick run

```powershell
cd .\samples\FraudDetection\src\FraudDetection.Api
dotnet restore
dotnet run
```

The sample listens on the default ASP.NET Core URLs. Swagger is enabled.

## Trigger a fraud alert

Send three transactions for the same card inside 60 seconds:

```powershell
curl -X POST http://localhost:5000/api/transactions/evaluate -H "Content-Type: application/json" -H "x-tenant-id: tenant-a" -d "{\"transactionId\":\"tx-001\",\"cardId\":\"card-01\",\"amount\":1200,\"merchantId\":\"m-01\",\"countryCode\":\"US\",\"timestampUtc\":\"2026-03-09T10:00:00Z\"}"
curl -X POST http://localhost:5000/api/transactions/evaluate -H "Content-Type: application/json" -H "x-tenant-id: tenant-a" -d "{\"transactionId\":\"tx-002\",\"cardId\":\"card-01\",\"amount\":980,\"merchantId\":\"m-01\",\"countryCode\":\"US\",\"timestampUtc\":\"2026-03-09T10:00:20Z\"}"
curl -X POST http://localhost:5000/api/transactions/evaluate -H "Content-Type: application/json" -H "x-tenant-id: tenant-a" -d "{\"transactionId\":\"tx-003\",\"cardId\":\"card-01\",\"amount\":760,\"merchantId\":\"m-01\",\"countryCode\":\"US\",\"timestampUtc\":\"2026-03-09T10:00:40Z\"}"
```

Expected on the third request:

- `alertTriggered: true`
- `eventCount: 3`

## Change the CEP window by API

The sample bootstraps a default config called `high-velocity-cards`, but you can override it through the built-in CEP controller:

```powershell
curl -X PUT http://localhost:5000/api/v1/rule-engine/cep/high-velocity-cards -H "Content-Type: application/json" -H "x-tenant-id: tenant-a" -d "{\"name\":\"High velocity cards\",\"description\":\"Three or more card events in 120 seconds.\",\"windowType\":\"Sliding\",\"windowSizeSeconds\":120,\"timeToLiveSeconds\":300,\"correlationKey\":\"cardId\",\"metadata\":{\"threshold\":\"3\"}}"
```

Repeat the transaction calls after updating the config and observe how the alert threshold and window size affect the outcome.
