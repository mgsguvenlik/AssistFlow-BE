-- İNCELEME PAKETİ: Yedek/prova ve onay olmadan çalıştırmayın.
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AssistFlow' THROW 51000, N'Yanlış hedef DB.', 1;
GO
IF ISNULL(OBJECT_ID(N'__EFMigrationsHistory'),-1)<>OBJECT_ID(N'dbo.__EFMigrationsHistory') THROW 51000, N'Migration varsayılan şeması uyuşmuyor.', 1;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260927123000_AddCollectionTrackingIndexes') THROW 51000, N'Önceki migration eksik.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160000_AddCollectionGroupParent'
)
BEGIN
    CREATE TABLE [collection].[GroupParent] (
        [CustomerId] bigint NOT NULL,
        [CustomerGroupId] bigint NOT NULL,
        [LegacyCustomerId] bigint NOT NULL,
        [AccountNo] nvarchar(200) NULL,
        [IsCorporate] bit NOT NULL,
        [SourceHash] binary(32) NOT NULL,
        [CreatedDate] datetimeoffset NOT NULL,
        [CreatedUser] bigint NOT NULL,
        CONSTRAINT [PK_GroupParent] PRIMARY KEY ([CustomerId]),
        CONSTRAINT [FK_GroupParent_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers] ([Id]),
        CONSTRAINT [FK_GroupParent_CustomerGroups_CustomerGroupId] FOREIGN KEY ([CustomerGroupId]) REFERENCES [dbo].[CustomerGroups] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160000_AddCollectionGroupParent'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_GroupParent_LegacyCustomerId] ON [collection].[GroupParent] ([LegacyCustomerId]) WHERE [LegacyCustomerId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160000_AddCollectionGroupParent'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_GroupParent_CustomerGroupId] ON [collection].[GroupParent] ([CustomerGroupId]) WHERE [CustomerGroupId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160000_AddCollectionGroupParent'
)
BEGIN
    CREATE INDEX [IX_GroupParent_AccountNo] ON [collection].[GroupParent] ([AccountNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927160000_AddCollectionGroupParent'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260927160000_AddCollectionGroupParent', N'9.0.5');
END;

COMMIT;
GO

