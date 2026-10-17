SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
    BEGIN TRANSACTION;
    IF DB_NAME() <> N'AssistFlowTest' THROW 51000, N'Hedef veritabanı uygun değil.', 1;
    DECLARE @result int;
    EXEC @result = sys.sp_getapplock @Resource=N'CollectionFoundationInstall', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
    IF @result < 0 THROW 51000, N'Kurulum kilidi alınamadı.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260912184617_AddCollectionOriginalAnchorDay')
        THROW 51000, N'Önceki tahsilat migration kaydı bulunamadı.', 1;
    IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260913081707_AddCollectionContractCreationRequest')
        THROW 51000, N'Migration zaten uygulanmış.', 1;
    IF COL_LENGTH(N'collection.Contract', N'CreationRequestId') IS NOT NULL OR COL_LENGTH(N'collection.Contract', N'CreationPayloadHash') IS NOT NULL
        THROW 51000, N'Kolonlar mevcut; şema uyumu incelenmeli.', 1;
    ALTER TABLE [collection].[Contract] ADD [CreationPayloadHash] varbinary(32) NULL;
    ALTER TABLE [collection].[Contract] ADD [CreationRequestId] uniqueidentifier NULL;
    EXEC(N'CREATE UNIQUE INDEX [UX_Contract_CreationRequestId] ON [collection].[Contract] ([CreationRequestId]) WHERE [CreationRequestId] IS NOT NULL;');
    EXEC(N'ALTER TABLE [collection].[Contract] ADD CONSTRAINT [CK_Contract_CreationRequest] CHECK (([CreationRequestId] IS NULL AND [CreationPayloadHash] IS NULL) OR ([CreationRequestId] IS NOT NULL AND [CreationPayloadHash] IS NOT NULL AND DATALENGTH([CreationPayloadHash]) = 32));');
    INSERT INTO dbo.__EFMigrationsHistory(MigrationId,ProductVersion) VALUES(N'20260913081707_AddCollectionContractCreationRequest',N'9.0.5');
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;
