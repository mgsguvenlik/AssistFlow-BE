-- İNCELEME PAKETİ: Yedek/prova ve onay olmadan çalıştırmayın.
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AssistFlow' THROW 51000, N'Yanlış hedef DB.', 1;
GO
IF ISNULL(OBJECT_ID(N'__EFMigrationsHistory'),-1)<>OBJECT_ID(N'dbo.__EFMigrationsHistory') THROW 51000, N'Migration varsayılan şeması uyuşmuyor.', 1;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260929080736_AddCollectionInvoices') THROW 51000, N'Önceki migration eksik.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929084242_AddCollectionInvoiceOperations'
)
BEGIN
    CREATE TABLE [collection].[InvoiceOperation] (
        [Id] bigint NOT NULL IDENTITY,
        [RequestId] uniqueidentifier NOT NULL,
        [ActorUserId] bigint NOT NULL,
        [Kind] nvarchar(30) NOT NULL,
        [InvoiceId] bigint NOT NULL,
        [PaymentId] bigint NULL,
        [LegacyInvoiceId] bigint NULL,
        [LegacyPaymentId] bigint NULL,
        [PayloadHash] binary(32) NOT NULL,
        [BeforeJson] nvarchar(max) NULL,
        [AfterJson] nvarchar(max) NULL,
        [CompletedDate] datetimeoffset NOT NULL,
        CONSTRAINT [PK_InvoiceOperation] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_InvoiceOperation_After] CHECK ([AfterJson] IS NULL OR ISJSON([AfterJson]) = 1),
        CONSTRAINT [CK_InvoiceOperation_Before] CHECK ([BeforeJson] IS NULL OR ISJSON([BeforeJson]) = 1),
        CONSTRAINT [CK_InvoiceOperation_Kind] CHECK ([Kind] IN ('PaymentCreate','PaymentUpdate','PaymentDelete','CommentUpdate','InvoiceDelete'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929084242_AddCollectionInvoiceOperations'
)
BEGIN
    CREATE INDEX [IX_InvoiceOperation_InvoiceId_CompletedDate_Id] ON [collection].[InvoiceOperation] ([InvoiceId], [CompletedDate], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929084242_AddCollectionInvoiceOperations'
)
BEGIN
    CREATE INDEX [IX_InvoiceOperation_LegacyInvoiceId_Kind] ON [collection].[InvoiceOperation] ([LegacyInvoiceId], [Kind]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929084242_AddCollectionInvoiceOperations'
)
BEGIN
    CREATE INDEX [IX_InvoiceOperation_LegacyPaymentId_Kind] ON [collection].[InvoiceOperation] ([LegacyPaymentId], [Kind]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929084242_AddCollectionInvoiceOperations'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InvoiceOperation_RequestId] ON [collection].[InvoiceOperation] ([RequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929084242_AddCollectionInvoiceOperations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929084242_AddCollectionInvoiceOperations', N'9.0.5');
END;

COMMIT;
GO

