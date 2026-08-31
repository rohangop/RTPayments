CREATE PROCEDURE [dbo].[RecordPaymentFailure]
    @PaymentId UNIQUEIDENTIFIER,
    @FailureReason NVARCHAR(1000)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[Payments]
    SET
        [Status] = 'Failed',
        [ProcessedAtUtc] = SYSUTCDATETIME(),
        [ProcessingStartedAtUtc] = NULL,
        [FailureReason] = @FailureReason
    WHERE [PaymentId] = @PaymentId
      AND [Status] = 'Processing';

    SELECT @@ROWCOUNT AS [RowsUpdated];
END;
GO
