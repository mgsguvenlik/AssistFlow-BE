-- İNCELEME PAKETİ: Yedek/prova ve onay olmadan çalıştırmayın.
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AssistFlow' THROW 51000, N'Yanlış hedef DB.', 1;
GO
IF ISNULL(OBJECT_ID(N'__EFMigrationsHistory'),-1)<>OBJECT_ID(N'dbo.__EFMigrationsHistory') THROW 51000, N'Migration varsayılan şeması uyuşmuyor.', 1;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260929130125_AddCollectionContractCorrections') THROW 51000, N'Önceki migration eksik.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930194336_AddCollectionCustomerAttachments'
)
BEGIN
    CREATE TABLE [collection].[CustomerAttachment] (
        [Id] bigint NOT NULL IDENTITY,
        [CustomerId] bigint NOT NULL,
        [LegacyCustomerId] bigint NOT NULL,
        [RetainedLegacyCustomerId] bigint NOT NULL,
        [SourcePath] nvarchar(500) NOT NULL,
        [ArchiveHash] nvarchar(64) NOT NULL,
        [ContentHash] nvarchar(64) NOT NULL,
        [OriginalFileName] nvarchar(260) NOT NULL,
        [StoredFileName] nvarchar(260) NOT NULL,
        [ContentType] nvarchar(100) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [Decision] nvarchar(1000) NOT NULL,
        [CreatedDate] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CustomerAttachment] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomerAttachment_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930194336_AddCollectionCustomerAttachments'
)
BEGIN
    CREATE INDEX [IX_CustomerAttachment_CustomerId_Id] ON [collection].[CustomerAttachment] ([CustomerId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930194336_AddCollectionCustomerAttachments'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomerAttachment_SourcePath] ON [collection].[CustomerAttachment] ([SourcePath]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930194336_AddCollectionCustomerAttachments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930194336_AddCollectionCustomerAttachments', N'9.0.5');
END;

COMMIT;
GO

