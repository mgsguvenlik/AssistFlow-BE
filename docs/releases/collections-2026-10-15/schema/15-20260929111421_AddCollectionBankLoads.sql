-- İNCELEME PAKETİ: Yedek/prova ve onay olmadan çalıştırmayın.
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AssistFlow' THROW 51000, N'Yanlış hedef DB.', 1;
GO
IF ISNULL(OBJECT_ID(N'__EFMigrationsHistory'),-1)<>OBJECT_ID(N'dbo.__EFMigrationsHistory') THROW 51000, N'Migration varsayılan şeması uyuşmuyor.', 1;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260929101445_AddCollectionInvoiceLoadRowAudit') THROW 51000, N'Önceki migration eksik.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929111421_AddCollectionBankLoads'
)
BEGIN
    CREATE TABLE [collection].[BankBaseline] (
        [Id] int NOT NULL,
        [SourceHash] nvarchar(64) NOT NULL,
        [SourceCount] bigint NOT NULL,
        [VerifiedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_BankBaseline] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929111421_AddCollectionBankLoads'
)
BEGIN
    CREATE TABLE [collection].[BankLoad] (
        [Id] bigint NOT NULL IDENTITY,
        [Type] nvarchar(3) NOT NULL,
        [FileHash] nvarchar(64) NOT NULL,
        [FileName] nvarchar(260) NOT NULL,
        [StoredFileName] nvarchar(260) NOT NULL,
        [Period] date NOT NULL,
        [CreatedUser] bigint NOT NULL,
        [CreatedDate] datetimeoffset NOT NULL,
        [ReviewedAt] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_BankLoad] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929111421_AddCollectionBankLoads'
)
BEGIN
    CREATE TABLE [collection].[BankTransaction] (
        [Key] nvarchar(64) NOT NULL,
        [Source] nvarchar(20) NOT NULL,
        [PaymentId] bigint NULL,
        [LoadRowId] bigint NULL,
        [RecordedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_BankTransaction] PRIMARY KEY ([Key])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929111421_AddCollectionBankLoads'
)
BEGIN
    CREATE TABLE [collection].[BankLoadRow] (
        [Id] bigint NOT NULL IDENTITY,
        [LoadId] bigint NOT NULL,
        [RowNumber] int NOT NULL,
        [SourceJson] nvarchar(max) NOT NULL,
        [TransactionKey] nvarchar(64) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Issue] nvarchar(4000) NULL,
        [ContractId] bigint NULL,
        [CurrencyTypeId] bigint NULL,
        [PaymentId] bigint NULL,
        [AppliedUser] bigint NULL,
        [AppliedAt] datetimeoffset NULL,
        CONSTRAINT [PK_BankLoadRow] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BankLoadRow_BankLoad_LoadId] FOREIGN KEY ([LoadId]) REFERENCES [collection].[BankLoad] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929111421_AddCollectionBankLoads'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BankLoad_Type_FileHash_Period] ON [collection].[BankLoad] ([Type], [FileHash], [Period]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929111421_AddCollectionBankLoads'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BankLoadRow_LoadId_RowNumber] ON [collection].[BankLoadRow] ([LoadId], [RowNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929111421_AddCollectionBankLoads'
)
BEGIN
    CREATE INDEX [IX_BankLoadRow_LoadId_Status_RowNumber] ON [collection].[BankLoadRow] ([LoadId], [Status], [RowNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929111421_AddCollectionBankLoads'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929111421_AddCollectionBankLoads', N'9.0.5');
END;

COMMIT;
GO

