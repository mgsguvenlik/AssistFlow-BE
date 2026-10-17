-- İNCELEME PAKETİ: Yedek/prova ve onay olmadan çalıştırmayın.
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AssistFlow' THROW 51000, N'Yanlış hedef DB.', 1;
GO
IF ISNULL(OBJECT_ID(N'__EFMigrationsHistory'),-1)<>OBJECT_ID(N'dbo.__EFMigrationsHistory') THROW 51000, N'Migration varsayılan şeması uyuşmuyor.', 1;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260929084242_AddCollectionInvoiceOperations') THROW 51000, N'Önceki migration eksik.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929100739_AddCollectionInvoiceLoads'
)
BEGIN
    CREATE TABLE [collection].[InvoiceAccount] (
        [Code] nvarchar(200) COLLATE Latin1_General_100_BIN2 NOT NULL,
        [CustomerId] bigint NULL,
        [Issue] nvarchar(500) NULL,
        [SourceIdsJson] nvarchar(max) NOT NULL,
        [SourceHash] nvarchar(64) NOT NULL,
        [VerifiedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_InvoiceAccount] PRIMARY KEY ([Code]),
        CONSTRAINT [FK_InvoiceAccount_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929100739_AddCollectionInvoiceLoads'
)
BEGIN
    CREATE TABLE [collection].[InvoiceLoad] (
        [Id] bigint NOT NULL IDENTITY,
        [Type] nvarchar(1) NOT NULL,
        [FileHash] nvarchar(64) NOT NULL,
        [FileName] nvarchar(260) NOT NULL,
        [StoredFileName] nvarchar(260) NOT NULL,
        [CreatedUser] bigint NOT NULL,
        [CreatedDate] datetimeoffset NOT NULL,
        [AppliedUser] bigint NULL,
        [AppliedAt] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_InvoiceLoad] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_InvoiceLoad_Type] CHECK ([Type] IN ('B','K'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929100739_AddCollectionInvoiceLoads'
)
BEGIN
    CREATE TABLE [collection].[InvoiceLoadRow] (
        [Id] bigint NOT NULL IDENTITY,
        [LoadId] bigint NOT NULL,
        [RowNumber] int NOT NULL,
        [SourceJson] nvarchar(max) NOT NULL,
        [CustomerId] bigint NULL,
        [CurrencyTypeId] bigint NULL,
        [Status] nvarchar(20) NOT NULL,
        [Issue] nvarchar(4000) NULL,
        [InvoiceId] bigint NULL,
        [ImportedKey] nvarchar(64) NULL,
        CONSTRAINT [PK_InvoiceLoadRow] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_InvoiceLoadRow_Status] CHECK ([Status] IN ('Ready','Error','Duplicate','Imported')),
        CONSTRAINT [FK_InvoiceLoadRow_InvoiceLoad_LoadId] FOREIGN KEY ([LoadId]) REFERENCES [collection].[InvoiceLoad] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929100739_AddCollectionInvoiceLoads'
)
BEGIN
    CREATE INDEX [IX_InvoiceAccount_CustomerId] ON [collection].[InvoiceAccount] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929100739_AddCollectionInvoiceLoads'
)
BEGIN
    CREATE INDEX [IX_InvoiceLoad_CreatedDate_Id] ON [collection].[InvoiceLoad] ([CreatedDate], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929100739_AddCollectionInvoiceLoads'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InvoiceLoad_Type_FileHash] ON [collection].[InvoiceLoad] ([Type], [FileHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929100739_AddCollectionInvoiceLoads'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_InvoiceLoadRow_ImportedKey] ON [collection].[InvoiceLoadRow] ([ImportedKey]) WHERE [ImportedKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929100739_AddCollectionInvoiceLoads'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InvoiceLoadRow_LoadId_RowNumber] ON [collection].[InvoiceLoadRow] ([LoadId], [RowNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929100739_AddCollectionInvoiceLoads'
)
BEGIN
    CREATE INDEX [IX_InvoiceLoadRow_LoadId_Status_RowNumber] ON [collection].[InvoiceLoadRow] ([LoadId], [Status], [RowNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929100739_AddCollectionInvoiceLoads'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929100739_AddCollectionInvoiceLoads', N'9.0.5');
END;

COMMIT;
GO

