-- Reviewed module-only deployment. No backfill; NULL preserves previous anchor-day behavior.
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
    BEGIN TRANSACTION;
    IF DB_NAME() <> N'AssistFlowTest' THROW 51000, N'Hedef veritabanı uygun değil.', 1;
    DECLARE @lockResult int;
    EXEC @lockResult = sys.sp_getapplock @Resource=N'CollectionFoundationInstall', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
    IF @lockResult < 0 THROW 51000, N'Kurulum kilidi alınamadı.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260910150310_AddCollectionFoundation')
        THROW 51000, N'Tahsilat temel migration kaydı bulunamadı.', 1;
    IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260912184617_AddCollectionOriginalAnchorDay')
        THROW 51000, N'Migration zaten uygulanmış; yeniden çalıştırılmadı.', 1;
    IF COL_LENGTH(N'collection.ContractRatePeriod', N'OriginalAnchorDay') IS NOT NULL
        THROW 51000, N'Kolon mevcut; şema uyumu incelenmeli.', 1;
    ALTER TABLE [collection].[ContractRatePeriod] ADD [OriginalAnchorDay] tinyint NULL;
    -- Separate dynamic batch resolves the column added in this transaction.
    EXEC(N'ALTER TABLE [collection].[ContractRatePeriod] ADD CONSTRAINT [CK_ContractRatePeriod_OriginalDay] CHECK ([OriginalAnchorDay] IS NULL OR [OriginalAnchorDay] BETWEEN 1 AND 31);');
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260912184617_AddCollectionOriginalAnchorDay', N'9.0.5');
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;
