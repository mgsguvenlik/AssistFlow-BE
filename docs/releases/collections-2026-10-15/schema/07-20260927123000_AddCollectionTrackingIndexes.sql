-- İNCELEME PAKETİ: Yedek/prova ve onay olmadan çalıştırmayın.
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AssistFlow' THROW 51000, N'Yanlış hedef DB.', 1;
GO
IF ISNULL(OBJECT_ID(N'__EFMigrationsHistory'),-1)<>OBJECT_ID(N'dbo.__EFMigrationsHistory') THROW 51000, N'Migration varsayılan şeması uyuşmuyor.', 1;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260916202500_AddCollectionContractAttachment') THROW 51000, N'Önceki migration eksik.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927123000_AddCollectionTrackingIndexes'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ContractRatePeriod_Tracking] ON [collection].[ContractRatePeriod] ([EffectiveFrom], [EffectiveToExclusive], [ContractId], [CurrencyTypeId]) WHERE [IsDeleted] = 0 AND [BillingBehavior] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927123000_AddCollectionTrackingIndexes'
)
BEGIN
    CREATE INDEX [IX_Payment_Period_Contract_Currency_Id] ON [collection].[Payment] ([Period], [ContractId], [CurrencyTypeId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927123000_AddCollectionTrackingIndexes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260927123000_AddCollectionTrackingIndexes', N'9.0.5');
END;

COMMIT;
GO

