# Payments database

This folder contains the SQL schema and stored procedures used by the payment-processing flow.

It is intentionally lightweight and does not require a SQL Database Project or DACPAC tooling.

Current contents:

- `Schema/Payments.sql` - payment records, constraints, and indexes.
- `StoredProcedures/TryCreateOrClaimPayment.sql` - creates or atomically claims a payment for processing.
- `StoredProcedures/RecordPaymentSuccess.sql` - marks a claimed payment as successful.
- `StoredProcedures/RecordPaymentFailure.sql` - marks a claimed payment as failed.
