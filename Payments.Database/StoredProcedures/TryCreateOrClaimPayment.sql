CREATE PROCEDURE [dbo].[TryCreateOrClaimPayment]
    @PaymentId UNIQUEIDENTIFIER,
    @BatchId UNIQUEIDENTIFIER,
    @BatchReference NVARCHAR(100),
    @PaymentReference NVARCHAR(100),
    @TreasuryAccountId NVARCHAR(100),
    @BeneficiaryName NVARCHAR(200),
    @BeneficiaryAccount NVARCHAR(200),
    @Currency CHAR(3),
    @Amount DECIMAL(19, 4),
    @SettlementDate DATE,
    @LeaseDurationMinutes INT = 5
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @Status NVARCHAR(30);
    DECLARE @ProcessingStartedAtUtc DATETIME2(7);

    SELECT
        @Status = [Status],
        @ProcessingStartedAtUtc = [ProcessingStartedAtUtc]
    FROM [dbo].[Payments] WITH (UPDLOCK, HOLDLOCK)
    WHERE [PaymentId] = @PaymentId;

    IF @Status IS NULL
    BEGIN
        INSERT INTO [dbo].[Payments]
        (
            [PaymentId],
            [BatchId],
            [BatchReference],
            [PaymentReference],
            [TreasuryAccountId],
            [BeneficiaryName],
            [BeneficiaryAccount],
            [Currency],
            [Amount],
            [SettlementDate],
            [Status],
            [AttemptCount],
            [ProcessingStartedAtUtc]
        )
        VALUES
        (
            @PaymentId,
            @BatchId,
            @BatchReference,
            @PaymentReference,
            @TreasuryAccountId,
            @BeneficiaryName,
            @BeneficiaryAccount,
            @Currency,
            @Amount,
            @SettlementDate,
            'Processing',
            1,
            SYSUTCDATETIME()
        );

        COMMIT TRANSACTION;
        SELECT 'Claimed' AS [Result];
        RETURN;
    END;

    IF @Status = 'Succeeded'
    BEGIN
        COMMIT TRANSACTION;
        SELECT 'AlreadySucceeded' AS [Result];
        RETURN;
    END;

    IF @Status = 'Processing'
       AND @ProcessingStartedAtUtc >= DATEADD(
           MINUTE,
           -@LeaseDurationMinutes,
           SYSUTCDATETIME())
    BEGIN
        COMMIT TRANSACTION;
        SELECT 'AlreadyProcessing' AS [Result];
        RETURN;
    END;

    UPDATE [dbo].[Payments]
    SET
        [Status] = 'Processing',
        [AttemptCount] = [AttemptCount] + 1,
        [ProcessingStartedAtUtc] = SYSUTCDATETIME()
    WHERE [PaymentId] = @PaymentId;

    COMMIT TRANSACTION;
    SELECT 'Claimed' AS [Result];
END;
GO
