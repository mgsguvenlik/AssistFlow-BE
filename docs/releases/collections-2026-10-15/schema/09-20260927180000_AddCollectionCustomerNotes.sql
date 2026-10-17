-- İNCELEME PAKETİ: Yedek/prova ve onay olmadan çalıştırmayın.
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AssistFlow' THROW 51000, N'Yanlış hedef DB.', 1;
GO
IF ISNULL(OBJECT_ID(N'__EFMigrationsHistory'),-1)<>OBJECT_ID(N'dbo.__EFMigrationsHistory') THROW 51000, N'Migration varsayılan şeması uyuşmuyor.', 1;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260927160000_AddCollectionGroupParent') THROW 51000, N'Önceki migration eksik.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927180000_AddCollectionCustomerNotes'
)
BEGIN
    CREATE TABLE [collection].[CustomerNote] (
        [Id] bigint NOT NULL IDENTITY,
        [CustomerId] bigint NOT NULL,
        [Text] nvarchar(max) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedDate] datetimeoffset NOT NULL,
        [UpdatedDate] datetimeoffset NULL,
        [CreatedUser] bigint NOT NULL,
        [UpdatedUser] bigint NULL,
        [IsDeleted] bit NOT NULL,
        [LegacyCommentId] bigint NULL,
        [LegacyCustomerId] bigint NULL,
        [LegacyCreatedBy] nvarchar(200) NULL,
        [LegacyModifiedBy] nvarchar(200) NULL,
        [LegacyCreatedOn] datetime2 NULL,
        [LegacyModifiedOn] datetime2 NULL,
        [SourceHash] binary(32) NULL,
        CONSTRAINT [PK_CustomerNote] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomerNote_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927180000_AddCollectionCustomerNotes'
)
BEGIN
    CREATE INDEX [IX_CustomerNote_CustomerId_IsDeleted_CreatedDate_Id] ON [collection].[CustomerNote] ([CustomerId], [IsDeleted], [CreatedDate], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927180000_AddCollectionCustomerNotes'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CustomerNote_LegacyCommentId] ON [collection].[CustomerNote] ([LegacyCommentId]) WHERE [LegacyCommentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260927180000_AddCollectionCustomerNotes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260927180000_AddCollectionCustomerNotes', N'9.0.5');
END;

COMMIT;
GO

