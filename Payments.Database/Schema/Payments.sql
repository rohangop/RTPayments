CREATE TABLE [dbo].[Payments]
(
    [PaymentId] UNIQUEIDENTIFIER NOT NULL,
    [BatchId] UNIQUEIDENTIFIER NOT NULL,
    [TreasuryAccountId] NVARCHAR(100) NOT NULL,
    [BeneficiaryName] NVARCHAR(200) NOT NULL,
    [BeneficiaryAccount] NVARCHAR(200) NOT NULL,
    [Currency] CHAR(3) NOT NULL,
    [Amount] DECIMAL(19, 4) NOT NULL,
    [SettlementDate] DATE NOT NULL,
    [Status] NVARCHAR(30) NOT NULL,
    [AttemptCount] INT NOT NULL
        CONSTRAINT [DF_Payments_AttemptCount] DEFAULT 0,
    [ProcessingStartedAtUtc] DATETIME2(7) NULL,
    [ProcessedAtUtc] DATETIME2(7) NULL,
    [FailureReason] NVARCHAR(1000) NULL,

    CONSTRAINT [PK_Payments] PRIMARY KEY ([PaymentId]),
    CONSTRAINT [CK_Payments_Amount]
        CHECK ([Amount] > 0),
    CONSTRAINT [CK_Payments_Status]
        CHECK ([Status] IN ('Pending', 'Processing', 'Succeeded', 'Failed'))
);
GO

CREATE INDEX [IX_Payments_BatchId]
    ON [dbo].[Payments] ([BatchId]);
GO

CREATE INDEX [IX_Payments_Status]
    ON [dbo].[Payments] ([Status]);
GO
