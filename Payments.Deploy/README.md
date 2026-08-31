# Payments deployment

This folder contains the Bicep templates and related deployment files for RTPayments.

The current template creates the two regional PaymentsFD App Services and their Linux App Service plans:

- East US
- West Europe

Azure Front Door will be added separately later.

The template also models the Functions tier as active-active:

- One Linux isolated-worker Function App in East US.
- One Linux isolated-worker Function App in West Europe.
- One regional storage account and consumption plan per Function App.

The template also creates one East US Service Bus Premium namespace with Geo-Replication configured to a West Europe secondary. The `payments` queue is defined on that namespace and is replicated with the namespace.

The replication configuration uses synchronous mode, providing an RPO of zero for acknowledged messages. There is one active primary region at a time; the West Europe region is a hot secondary and must be promoted during a regional failover.

The Function currently relies on five Service Bus delivery attempts for bounded retries. Ideally, retryable failures would use an exponential backoff policy before redelivery.

The template also creates Azure SQL Database with:

- An East US logical server and primary `Payments` database.
- A West Europe logical server and passive geo-secondary database.
- A manual SQL failover group with a stable read-write listener.

Azure SQL active geo-replication is asynchronous. A regional failure can therefore lose a small number of recently committed payment entries that have not reached the secondary. If a payment was accepted externally before the database state replicated, replay or reconciliation can also result in a small number of duplicate payments. This is an acknowledged residual risk for the exercise; a production design would use provider idempotency and reconciliation to resolve ambiguous outcomes. A complete regional Azure outage is considered a rare event.

## Follow-up work

- Add Azure Front Door routing for the East US and West Europe PaymentsFD apps.
- Add managed identities and Service Bus RBAC assignments for application authentication.
