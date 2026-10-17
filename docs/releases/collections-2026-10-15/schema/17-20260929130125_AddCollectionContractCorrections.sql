-- İNCELEME PAKETİ: Yedek/prova ve onay olmadan çalıştırmayın.
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AssistFlow' THROW 51000, N'Yanlış hedef DB.', 1;
GO
IF ISNULL(OBJECT_ID(N'__EFMigrationsHistory'),-1)<>OBJECT_ID(N'dbo.__EFMigrationsHistory') THROW 51000, N'Migration varsayılan şeması uyuşmuyor.', 1;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260929112405_AddCollectionBankReferenceIndexes') THROW 51000, N'Önceki migration eksik.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929130125_AddCollectionContractCorrections'
)
BEGIN
    CREATE TABLE [collection].[ContractCorrection] (
        [Id] bigint NOT NULL IDENTITY,
        [ContractId] bigint NOT NULL,
        [RequestId] uniqueidentifier NOT NULL,
        [PayloadHash] nvarchar(64) NOT NULL,
        [Kind] nvarchar(20) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [BeforeJson] nvarchar(max) NOT NULL,
        [AfterJson] nvarchar(max) NOT NULL,
        [ActorUserId] bigint NOT NULL,
        [CreatedDate] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ContractCorrection] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929130125_AddCollectionContractCorrections'
)
BEGIN
    CREATE INDEX [IX_ContractCorrection_ContractId_Id] ON [collection].[ContractCorrection] ([ContractId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929130125_AddCollectionContractCorrections'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ContractCorrection_RequestId] ON [collection].[ContractCorrection] ([RequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929130125_AddCollectionContractCorrections'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929130125_AddCollectionContractCorrections', N'9.0.5');
END;

COMMIT;
GO

