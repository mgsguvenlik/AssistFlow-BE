-- İNCELEME PAKETİ: Yedek/prova ve onay olmadan çalıştırmayın.
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AssistFlow' THROW 51000, N'Yanlış hedef DB.', 1;
GO
IF ISNULL(OBJECT_ID(N'__EFMigrationsHistory'),-1)<>OBJECT_ID(N'dbo.__EFMigrationsHistory') THROW 51000, N'Migration varsayılan şeması uyuşmuyor.', 1;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260927180000_AddCollectionCustomerNotes') THROW 51000, N'Önceki migration eksik.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927190000_AddCollectionGroupFollowUpSource'
)
BEGIN
    ALTER TABLE [collection].[ContractPeriodFollowUp] ADD [LegacyFollowGroupStatusId] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927190000_AddCollectionGroupFollowUpSource'
)
BEGIN
    ALTER TABLE [collection].[ContractPeriodFollowUp] ADD [SourceHash] binary(32) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927190000_AddCollectionGroupFollowUpSource'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ContractPeriodFollowUp_LegacyFollowGroupStatusId] ON [collection].[ContractPeriodFollowUp] ([LegacyFollowGroupStatusId]) WHERE [LegacyFollowGroupStatusId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927190000_AddCollectionGroupFollowUpSource'
)
BEGIN
    CREATE INDEX [IX_ContractPeriodFollowUp_IsDeleted_Period_Id] ON [collection].[ContractPeriodFollowUp] ([IsDeleted], [Period], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927190000_AddCollectionGroupFollowUpSource'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260927190000_AddCollectionGroupFollowUpSource', N'9.0.5');
END;

COMMIT;
GO

