-- İNCELEME PAKETİ: Yedek/prova ve onay olmadan çalıştırmayın.
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AssistFlow' THROW 51000, N'Yanlış hedef DB.', 1;
GO
IF ISNULL(OBJECT_ID(N'__EFMigrationsHistory'),-1)<>OBJECT_ID(N'dbo.__EFMigrationsHistory') THROW 51000, N'Migration varsayılan şeması uyuşmuyor.', 1;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260910150310_AddCollectionFoundation') THROW 51000, N'Önceki migration eksik.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260912184617_AddCollectionOriginalAnchorDay'
)
BEGIN
    ALTER TABLE [collection].[ContractRatePeriod] ADD [OriginalAnchorDay] tinyint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260912184617_AddCollectionOriginalAnchorDay'
)
BEGIN
    EXEC(N'ALTER TABLE [collection].[ContractRatePeriod] ADD CONSTRAINT [CK_ContractRatePeriod_OriginalDay] CHECK ([OriginalAnchorDay] IS NULL OR [OriginalAnchorDay] BETWEEN 1 AND 31)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260912184617_AddCollectionOriginalAnchorDay'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260912184617_AddCollectionOriginalAnchorDay', N'9.0.5');
END;

COMMIT;
GO

