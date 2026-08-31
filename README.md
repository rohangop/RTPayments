# RTPayments

RTPayments is an Azure-based payment batch processing solution. It accepts a batch of payments, places the work on Azure Service Bus, and processes each payment independently through Azure Functions and Azure SQL.

The design uses **at-least-once delivery with durable idempotency**. It does not attempt to provide exactly-once execution across the message broker, database, and an external payment provider.

## Why Service Bus is the ingestion boundary

An alternative design was considered where PaymentsFD would write every payment directly to SQL and a SQL change feed would drive downstream processing. That would make SQL the initial ingestion point, but a large batch could create a burst of database writes at request time. Many concurrent submissions could therefore concentrate both ingestion and processing-state traffic on SQL before the system had a chance to smooth the workload.

This design publishes payment work to Service Bus first. The queue absorbs bursts and lets Functions apply SQL writes at a controlled processing rate, protecting the database from an immediate batch-sized write spike. The tradeoff is that payment state is not visible in SQL until the worker consumes the message, and Service Bus becomes an important availability and operational dependency.

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

## Availability and resilience

The deployment is designed to continue accepting and processing work through a regional failure, subject to the failover actions and limitations below:

```text
                    +--> PaymentsFD - East US --+
Client --> Front Door                            +--> Service Bus primary
                    +--> PaymentsFD - West Europe+
                                                        |
                                                        v
                                             Functions in both regions
                                                        |
                                                        v
                                             Azure SQL primary database
                                                        |
                                                        v
                                             SQL geo-secondary
```

- **API availability:** PaymentsFD is deployed in East US and West Europe so either region can serve requests. Azure Front Door is the intended global entry point and health-based router, but it is not yet included in the Bicep deployment.
- **Processing availability:** Function Apps are deployed in both regions and consume from the shared replicated queue. Service Bus distributes work to available consumers, allowing the processing tier to scale independently from the API tier.
- **Durable buffering:** The API publishes to Service Bus before returning `202 Accepted`. If Functions or SQL are temporarily unavailable, messages remain in the queue and can be retried instead of being lost in the API process.
- **Message-store resilience:** The Service Bus Premium namespace is configured with Geo-Replication from East US to West Europe. Replication is modeled synchronously for acknowledged messages, but only one namespace region is active at a time; regional failover requires promoting the secondary.
- **Database resilience:** Azure SQL has an East US primary and a West Europe geo-secondary behind a failover group with a stable read-write listener. SQL geo-replication is asynchronous, so the database has a non-zero disaster-recovery RPO even though the queue is modeled with zero replication lag.
- **Failure isolation:** Each payment is a separate queue message. A failed payment is retried independently and does not block unrelated payments in the same request.
- **Idempotent recovery:** Stable `BatchId` and `PaymentId` values, Service Bus duplicate detection, and SQL create-or-claim logic allow ambiguous client retries and message redelivery to be handled without treating the same payment as new work.

This is a **disaster-recovery and at-least-once processing design**, not a claim of zero downtime or exactly-once payment execution. A regional SQL failure can lose recently committed state that has not replicated, and a provider call can succeed immediately before the worker fails. Provider-side idempotency and reconciliation are therefore required for production-grade recovery from ambiguous outcomes.

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

### Batch size tradeoff

The API limits a request to 100 payments. This is an exercise-level boundary that keeps a logical request small enough to attempt as one Service Bus message batch, assuming payment messages remain around the expected payload size. The publisher still performs the authoritative `TryAddMessage` size check because 100 messages are not guaranteed to fit if individual messages become large.

Keeping the request as one broker batch and one send avoids splitting a client request across multiple independent sends, which could create partial publication if a later send fails. The tradeoff is that oversized requests are rejected rather than automatically split, so clients must submit smaller batches. A production design handling larger or variable payment payloads could split work deliberately, use an ingestion message that the Functions tier expands, or use blob-backed payloads, but each option adds coordination and operational complexity.

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
