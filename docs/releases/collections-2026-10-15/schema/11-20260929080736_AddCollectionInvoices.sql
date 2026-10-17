-- İNCELEME PAKETİ: Yedek/prova ve onay olmadan çalıştırmayın.
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AssistFlow' THROW 51000, N'Yanlış hedef DB.', 1;
GO
IF ISNULL(OBJECT_ID(N'__EFMigrationsHistory'),-1)<>OBJECT_ID(N'dbo.__EFMigrationsHistory') THROW 51000, N'Migration varsayılan şeması uyuşmuyor.', 1;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260927190000_AddCollectionGroupFollowUpSource') THROW 51000, N'Önceki migration eksik.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929080736_AddCollectionInvoices'
)
BEGIN
    CREATE TABLE [collection].[Invoice] (
        [Id] bigint NOT NULL IDENTITY,
        [CustomerId] bigint NOT NULL,
        [Type] nvarchar(1) NOT NULL,
        [Number] nvarchar(50) NULL,
        [Date] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [CurrencyTypeId] bigint NOT NULL,
        [ProjectCode] nvarchar(100) NULL,
        [Comment] nvarchar(max) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedDate] datetimeoffset NOT NULL,
        [CreatedUser] bigint NOT NULL,
        [LegacyInvoiceFollowId] bigint NULL,
        [LegacyCustomerId] bigint NULL,
        [LegacyCreatedBy] nvarchar(100) NULL,
        [LegacyModifiedBy] nvarchar(100) NULL,
        [LegacyCreatedOn] datetime2 NULL,
        [LegacyModifiedOn] datetime2 NULL,
        [SourceHash] binary(32) NULL,
        CONSTRAINT [PK_Invoice] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Invoice_Type] CHECK ([Type] IN ('B', 'K')),
        CONSTRAINT [FK_Invoice_CurrencyType_CurrencyTypeId] FOREIGN KEY ([CurrencyTypeId]) REFERENCES [CurrencyType] ([Id]),
        CONSTRAINT [FK_Invoice_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929080736_AddCollectionInvoices'
)
BEGIN
    CREATE TABLE [collection].[InvoicePayment] (
        [Id] bigint NOT NULL IDENTITY,
        [InvoiceId] bigint NOT NULL,
        [Date] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedDate] datetimeoffset NOT NULL,
        [CreatedUser] bigint NOT NULL,
        [LegacyInvoiceFollowPaymentId] bigint NULL,
        [LegacyCustomerId] bigint NULL,
        [LegacyCreatedBy] nvarchar(100) NULL,
        [LegacyModifiedBy] nvarchar(100) NULL,
        [LegacyCreatedOn] datetime2 NULL,
        [LegacyModifiedOn] datetime2 NULL,
        [SourceHash] binary(32) NULL,
        CONSTRAINT [PK_InvoicePayment] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InvoicePayment_Invoice_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [collection].[Invoice] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929080736_AddCollectionInvoices'
)
BEGIN
    CREATE INDEX [IX_Invoice_CurrencyTypeId] ON [collection].[Invoice] ([CurrencyTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929080736_AddCollectionInvoices'
)
BEGIN
    CREATE INDEX [IX_Invoice_CustomerId_Date_Id] ON [collection].[Invoice] ([CustomerId], [Date], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929080736_AddCollectionInvoices'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Invoice_LegacyInvoiceFollowId] ON [collection].[Invoice] ([LegacyInvoiceFollowId]) WHERE [LegacyInvoiceFollowId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929080736_AddCollectionInvoices'
)
BEGIN
    CREATE INDEX [IX_Invoice_Type_Date_Id] ON [collection].[Invoice] ([Type], [Date], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929080736_AddCollectionInvoices'
)
BEGIN
    CREATE INDEX [IX_InvoicePayment_InvoiceId_Date_Id] ON [collection].[InvoicePayment] ([InvoiceId], [Date], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929080736_AddCollectionInvoices'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_InvoicePayment_LegacyInvoiceFollowPaymentId] ON [collection].[InvoicePayment] ([LegacyInvoiceFollowPaymentId]) WHERE [LegacyInvoiceFollowPaymentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929080736_AddCollectionInvoices'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929080736_AddCollectionInvoices', N'9.0.5');
END;

COMMIT;
GO

