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

## Follow-up work

- Add Azure Front Door routing for the East US and West Europe PaymentsFD apps.
- Add managed identities and Service Bus RBAC assignments for application authentication.
