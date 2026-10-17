-- İNCELEME PAKETİ: Yedek/prova ve onay olmadan çalıştırmayın.
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AssistFlow' THROW 51000, N'Yanlış hedef DB.', 1;
GO
IF ISNULL(OBJECT_ID(N'__EFMigrationsHistory'),-1)<>OBJECT_ID(N'dbo.__EFMigrationsHistory') THROW 51000, N'Migration varsayılan şeması uyuşmuyor.', 1;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260929100739_AddCollectionInvoiceLoads') THROW 51000, N'Önceki migration eksik.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929101445_AddCollectionInvoiceLoadRowAudit'
)
BEGIN
    ALTER TABLE [collection].[InvoiceLoadRow] ADD [AppliedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929101445_AddCollectionInvoiceLoadRowAudit'
)
BEGIN
    ALTER TABLE [collection].[InvoiceLoadRow] ADD [AppliedUser] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260929101445_AddCollectionInvoiceLoadRowAudit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260929101445_AddCollectionInvoiceLoadRowAudit', N'9.0.5');
END;

COMMIT;
GO

