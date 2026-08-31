CREATE PROCEDURE [dbo].[RecordPaymentSuccess]
    @PaymentId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[Payments]
    SET
        [Status] = 'Succeeded',
        [ProcessedAtUtc] = SYSUTCDATETIME(),
        [ProcessingStartedAtUtc] = NULL,
        [FailureReason] = NULL
    WHERE [PaymentId] = @PaymentId
      AND [Status] = 'Processing';

    SELECT @@ROWCOUNT AS [RowsUpdated];
END;
GO
