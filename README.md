# RTPayments

RTPayments is an Azure-based payment batch processing solution. It accepts a batch of payments, places the work on Azure Service Bus, and processes each payment independently through Azure Functions and Azure SQL.

The design uses **at-least-once delivery with durable idempotency**. It does not attempt to provide exactly-once execution across the message broker, database, and an external payment provider.

## Architecture

```text
Client
  |
  v
PaymentsFD (ASP.NET Core API)
  |
  v
Azure Service Bus - payments queue
  |
  v
Payments.Functions (Azure Functions)
  |
  +--> Azure SQL Payments table
  |
  +--> Payment provider
```

The solution is organized into these projects:

- `PaymentsFD` - API that validates and accepts payment batches.
- `Payments.ServiceBus` - Shared queue contract and Azure Service Bus publisher.
- `Payments.Functions` - Service Bus-triggered payment worker.
- `Payments.Database` - SQL schema and stored procedures for payment state and idempotency.
- `Payments.Deploy` - Azure Bicep infrastructure definitions and deployment notes.

## Request flow

1. The client submits a batch to `POST /api/PaymentBatches`.
2. The request contains a client-generated `BatchId`.
3. Every payment contains a client-generated unique `PaymentId`.
4. The API accepts at most 100 payments per request.
5. The API creates one Service Bus message per payment.
6. Each message uses the payment ID as its Service Bus `MessageId` and the batch ID as its `CorrelationId`.
7. The publisher attempts to place all messages into one Service Bus message batch and sends it once.
8. If the messages do not fit in the Service Bus batch size limit, the request is rejected rather than split across multiple sends.
9. The API returns `202 Accepted` after the Service Bus send succeeds.
10. Azure Functions processes each payment message independently.

Example request:

```json
{
  "batchId": "11111111-1111-1111-1111-111111111111",
  "treasuryAccountId": "treasury-001",
  "settlementDate": "2026-08-31",
  "payments": [
    {
      "paymentId": "22222222-2222-2222-2222-222222222222",
      "beneficiaryName": "Example Beneficiary",
      "beneficiaryAccount": "account-001",
      "currency": "USD",
      "amount": 125.50,
      "description": "Example payment"
    }
  ]
}
```

### Client retry behavior

A `4xx` response is a validation or request error. The API rejects the request before publishing it, so the client should correct the request rather than retry it.

For a timeout, connection failure, or `5xx` response, the client cannot always know whether Service Bus accepted the batch. In that ambiguous case, the client should retry with the same `BatchId` and `PaymentId` values. Reusing the IDs lets Service Bus duplicate detection and the SQL idempotency logic recognize the retry as the same logical work. Generating new IDs would create a new batch and new payments.

## Idempotency and retries

Service Bus duplicate detection provides short-term broker-level duplicate suppression using `PaymentId` as `MessageId`. SQL is the durable idempotency boundary:

- `PaymentId` is the primary key in `dbo.Payments`.
- `TryCreateOrClaimPayment` atomically creates or claims a payment.
- A succeeded payment is ignored when its message is delivered again.
- An active processing lease prevents concurrent processing.
- An expired lease can be reclaimed.

The Function calls the payment provider only after successfully claiming the payment in SQL. On provider failure, it records the failure and throws an exception so Service Bus can redeliver the message. The queue is configured for five delivery attempts; messages that continue to fail become candidates for dead-lettering.

If the provider succeeds but the Function fails before recording SQL success, the message can be delivered again. A production provider integration should accept `PaymentId` as an idempotency key and support reconciliation for ambiguous outcomes.

## Azure deployment model

`Payments.Deploy/main.bicep` models:

- PaymentsFD App Services in East US and West Europe.
- Azure Functions in both regions.
- An East US Service Bus Premium namespace with a West Europe Geo-Replication secondary.
- A replicated `payments` queue.
- Azure SQL primary and geo-secondary databases with a failover group.

Service Bus Geo-Replication is modeled as active-passive, with one active primary region at a time. Azure SQL geo-replication is asynchronous, so a regional disaster can result in a small amount of data loss or ambiguous payment state. Provider idempotency and reconciliation are required to address that production risk.

Azure Front Door, managed identities, and RBAC assignments are follow-up deployment work. Connection strings in the application projects are placeholders and must be supplied through deployment configuration or application settings.

## Running locally

From the repository root:

```powershell
dotnet build RTPayments.slnx
```

The API and Function projects expect Service Bus and SQL configuration through application settings. The payment provider is currently a mock implementation that always succeeds.

Database scripts are under `Payments.Database` and should be applied to the target Azure SQL database before running the Function worker.

## Design boundaries

The current exercise intentionally keeps the implementation small:

- No SQL write occurs in PaymentsFD.
- No separate batch table is used; batch grouping is represented by `BatchId` on payment rows.
- No ledger or real payment provider integration is included yet.
- No automatic splitting of oversized requests is performed.
- No scheduled retry or exponential backoff policy is implemented.
- No batch-status endpoint is implemented.
