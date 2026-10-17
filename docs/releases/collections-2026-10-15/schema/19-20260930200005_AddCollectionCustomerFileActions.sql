-- İNCELEME PAKETİ: Yedek/prova ve onay olmadan çalıştırmayın.
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AssistFlow' THROW 51000, N'Yanlış hedef DB.', 1;
GO
IF ISNULL(OBJECT_ID(N'__EFMigrationsHistory'),-1)<>OBJECT_ID(N'dbo.__EFMigrationsHistory') THROW 51000, N'Migration varsayılan şeması uyuşmuyor.', 1;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260930194336_AddCollectionCustomerAttachments') THROW 51000, N'Önceki migration eksik.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930200005_AddCollectionCustomerFileActions'
)
BEGIN
    DECLARE @var sysname;
    SELECT @var = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[collection].[CustomerAttachment]') AND [c].[name] = N'RetainedLegacyCustomerId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [collection].[CustomerAttachment] DROP CONSTRAINT [' + @var + '];');
    ALTER TABLE [collection].[CustomerAttachment] ALTER COLUMN [RetainedLegacyCustomerId] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930200005_AddCollectionCustomerFileActions'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[collection].[CustomerAttachment]') AND [c].[name] = N'LegacyCustomerId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [collection].[CustomerAttachment] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [collection].[CustomerAttachment] ALTER COLUMN [LegacyCustomerId] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930200005_AddCollectionCustomerFileActions'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[collection].[CustomerAttachment]') AND [c].[name] = N'ArchiveHash');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [collection].[CustomerAttachment] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [collection].[CustomerAttachment] ALTER COLUMN [ArchiveHash] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930200005_AddCollectionCustomerFileActions'
)
BEGIN
    ALTER TABLE [collection].[CustomerAttachment] ADD [CreatedUser] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930200005_AddCollectionCustomerFileActions'
)
BEGIN
    ALTER TABLE [collection].[CustomerAttachment] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930200005_AddCollectionCustomerFileActions'
)
BEGIN
    ALTER TABLE [collection].[CustomerAttachment] ADD [RemovedDate] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930200005_AddCollectionCustomerFileActions'
)
BEGIN
    ALTER TABLE [collection].[CustomerAttachment] ADD [RemovedUser] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930200005_AddCollectionCustomerFileActions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930200005_AddCollectionCustomerFileActions', N'9.0.5');
END;

COMMIT;
GO

