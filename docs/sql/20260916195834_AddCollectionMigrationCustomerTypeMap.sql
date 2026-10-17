BEGIN TRANSACTION;
ALTER TABLE [collection].[MigrationReferenceMap] DROP CONSTRAINT [CK_MigrationReferenceMap_Target];

ALTER TABLE [collection].[MigrationReferenceMap] ADD [TargetCustomerTypeId] bigint NULL;

CREATE INDEX [IX_MigrationReferenceMap_TargetCustomerTypeId] ON [collection].[MigrationReferenceMap] ([TargetCustomerTypeId]);

ALTER TABLE [collection].[MigrationReferenceMap] ADD CONSTRAINT [CK_MigrationReferenceMap_Kind] CHECK ([Status] <> 1 OR ([ReferenceKind] = N'Customer' AND [TargetCustomerId] IS NOT NULL) OR ([ReferenceKind] = N'CustomerType' AND [TargetCustomerTypeId] IS NOT NULL) OR ([ReferenceKind] = N'ServiceType' AND [TargetServiceTypeId] IS NOT NULL) OR ([ReferenceKind] = N'CurrencyType' AND [TargetCurrencyTypeId] IS NOT NULL) OR ([ReferenceKind] = N'PaymentFrequency' AND [TargetPaymentFrequencyId] IS NOT NULL) OR ([ReferenceKind] = N'PaymentMethod' AND [TargetPaymentMethodId] IS NOT NULL) OR ([ReferenceKind] = N'SubscriptionStatus' AND [TargetSubscriptionStatusId] IS NOT NULL) OR ([ReferenceKind] = N'ContractStatus' AND [TargetContractStatusId] IS NOT NULL));

ALTER TABLE [collection].[MigrationReferenceMap] ADD CONSTRAINT [CK_MigrationReferenceMap_Target] CHECK ([Status] <> 1 OR (CASE WHEN [TargetCustomerId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetCustomerTypeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetServiceTypeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetCurrencyTypeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetPaymentFrequencyId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetPaymentMethodId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetSubscriptionStatusId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetContractStatusId] IS NULL THEN 0 ELSE 1 END) = 1);

ALTER TABLE [collection].[MigrationReferenceMap] ADD CONSTRAINT [FK_MigrationReferenceMap_CustomerType_TargetCustomerTypeId] FOREIGN KEY ([TargetCustomerTypeId]) REFERENCES [CustomerType] ([Id]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260916195834_AddCollectionMigrationCustomerTypeMap', N'9.0.5');

COMMIT;
GO

