-- İNCELEME PAKETİ: Yedek/prova ve onay olmadan çalıştırmayın.
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AssistFlow' THROW 51000, N'Yanlış hedef DB.', 1;
GO
IF ISNULL(OBJECT_ID(N'__EFMigrationsHistory'),-1)<>OBJECT_ID(N'dbo.__EFMigrationsHistory') THROW 51000, N'Migration varsayılan şeması uyuşmuyor.', 1;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260912184617_AddCollectionOriginalAnchorDay') THROW 51000, N'Önceki migration eksik.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913081707_AddCollectionContractCreationRequest'
)
BEGIN
    ALTER TABLE [collection].[Contract] ADD [CreationPayloadHash] varbinary(32) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913081707_AddCollectionContractCreationRequest'
)
BEGIN
    ALTER TABLE [collection].[Contract] ADD [CreationRequestId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913081707_AddCollectionContractCreationRequest'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Contract_CreationRequestId] ON [collection].[Contract] ([CreationRequestId]) WHERE [CreationRequestId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913081707_AddCollectionContractCreationRequest'
)
BEGIN
    EXEC(N'ALTER TABLE [collection].[Contract] ADD CONSTRAINT [CK_Contract_CreationRequest] CHECK (([CreationRequestId] IS NULL AND [CreationPayloadHash] IS NULL) OR ([CreationRequestId] IS NOT NULL AND [CreationPayloadHash] IS NOT NULL AND DATALENGTH([CreationPayloadHash]) = 32))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260913081707_AddCollectionContractCreationRequest'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260913081707_AddCollectionContractCreationRequest', N'9.0.5');
END;

COMMIT;
GO

