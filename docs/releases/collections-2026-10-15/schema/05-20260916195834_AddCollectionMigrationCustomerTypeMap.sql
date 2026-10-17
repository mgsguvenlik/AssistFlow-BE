-- İNCELEME PAKETİ: Yedek/prova ve onay olmadan çalıştırmayın.
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AssistFlow' THROW 51000, N'Yanlış hedef DB.', 1;
GO
IF ISNULL(OBJECT_ID(N'__EFMigrationsHistory'),-1)<>OBJECT_ID(N'dbo.__EFMigrationsHistory') THROW 51000, N'Migration varsayılan şeması uyuşmuyor.', 1;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260915180434_AddCollectionMigrationStaging') THROW 51000, N'Önceki migration eksik.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260916195834_AddCollectionMigrationCustomerTypeMap'
)
BEGIN
    ALTER TABLE [collection].[MigrationReferenceMap] DROP CONSTRAINT [CK_MigrationReferenceMap_Target];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260916195834_AddCollectionMigrationCustomerTypeMap'
)
BEGIN
    ALTER TABLE [collection].[MigrationReferenceMap] ADD [TargetCustomerTypeId] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260916195834_AddCollectionMigrationCustomerTypeMap'
)
BEGIN
    CREATE INDEX [IX_MigrationReferenceMap_TargetCustomerTypeId] ON [collection].[MigrationReferenceMap] ([TargetCustomerTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260916195834_AddCollectionMigrationCustomerTypeMap'
)
BEGIN
    EXEC(N'ALTER TABLE [collection].[MigrationReferenceMap] ADD CONSTRAINT [CK_MigrationReferenceMap_Kind] CHECK ([Status] <> 1 OR ([ReferenceKind] = N''Customer'' AND [TargetCustomerId] IS NOT NULL) OR ([ReferenceKind] = N''CustomerType'' AND [TargetCustomerTypeId] IS NOT NULL) OR ([ReferenceKind] = N''ServiceType'' AND [TargetServiceTypeId] IS NOT NULL) OR ([ReferenceKind] = N''CurrencyType'' AND [TargetCurrencyTypeId] IS NOT NULL) OR ([ReferenceKind] = N''PaymentFrequency'' AND [TargetPaymentFrequencyId] IS NOT NULL) OR ([ReferenceKind] = N''PaymentMethod'' AND [TargetPaymentMethodId] IS NOT NULL) OR ([ReferenceKind] = N''SubscriptionStatus'' AND [TargetSubscriptionStatusId] IS NOT NULL) OR ([ReferenceKind] = N''ContractStatus'' AND [TargetContractStatusId] IS NOT NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260916195834_AddCollectionMigrationCustomerTypeMap'
)
BEGIN
    EXEC(N'ALTER TABLE [collection].[MigrationReferenceMap] ADD CONSTRAINT [CK_MigrationReferenceMap_Target] CHECK ([Status] <> 1 OR (CASE WHEN [TargetCustomerId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetCustomerTypeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetServiceTypeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetCurrencyTypeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetPaymentFrequencyId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetPaymentMethodId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetSubscriptionStatusId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetContractStatusId] IS NULL THEN 0 ELSE 1 END) = 1)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260916195834_AddCollectionMigrationCustomerTypeMap'
)
BEGIN
    ALTER TABLE [collection].[MigrationReferenceMap] ADD CONSTRAINT [FK_MigrationReferenceMap_CustomerType_TargetCustomerTypeId] FOREIGN KEY ([TargetCustomerTypeId]) REFERENCES [CustomerType] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260916195834_AddCollectionMigrationCustomerTypeMap'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260916195834_AddCollectionMigrationCustomerTypeMap', N'9.0.5');
END;

COMMIT;
GO

