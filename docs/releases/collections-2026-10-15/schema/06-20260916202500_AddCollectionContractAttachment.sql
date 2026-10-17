-- İNCELEME PAKETİ: Yedek/prova ve onay olmadan çalıştırmayın.
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AssistFlow' THROW 51000, N'Yanlış hedef DB.', 1;
GO
IF ISNULL(OBJECT_ID(N'__EFMigrationsHistory'),-1)<>OBJECT_ID(N'dbo.__EFMigrationsHistory') THROW 51000, N'Migration varsayılan şeması uyuşmuyor.', 1;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260916195834_AddCollectionMigrationCustomerTypeMap') THROW 51000, N'Önceki migration eksik.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260916202500_AddCollectionContractAttachment'
)
BEGIN
    CREATE TABLE [collection].[ContractAttachment] (
        [Id] bigint NOT NULL IDENTITY,
        [ContractId] bigint NOT NULL,
        [OriginalFileName] nvarchar(260) NOT NULL,
        [StoredFileName] nvarchar(260) NOT NULL,
        [ContentType] nvarchar(150) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [CreatedDate] datetimeoffset NOT NULL,
        [UpdatedDate] datetimeoffset NULL,
        [CreatedUser] bigint NOT NULL,
        [UpdatedUser] bigint NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_ContractAttachment] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ContractAttachment_Size] CHECK ([SizeBytes] > 0 AND [SizeBytes] <= 20971520),
        CONSTRAINT [FK_ContractAttachment_Contract_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [collection].[Contract] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260916202500_AddCollectionContractAttachment'
)
BEGIN
    CREATE INDEX [IX_ContractAttachment_Contract_Deleted_Id] ON [collection].[ContractAttachment] ([ContractId], [IsDeleted], [Id] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260916202500_AddCollectionContractAttachment'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ContractAttachment_StoredFileName] ON [collection].[ContractAttachment] ([StoredFileName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260916202500_AddCollectionContractAttachment'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260916202500_AddCollectionContractAttachment', N'9.0.5');
END;

COMMIT;
GO

