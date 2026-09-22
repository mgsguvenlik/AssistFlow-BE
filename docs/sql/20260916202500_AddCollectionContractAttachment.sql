BEGIN TRANSACTION;
CREATE TABLE [collection].[ContractAttachment] (
    [Id] bigint NOT NULL IDENTITY,
    [ContractId] bigint NOT NULL,
    [OriginalFileName] nvarchar(260) NOT NULL,
    [StoredFileName] nvarchar(260) NOT NULL,
    [ContentType] nvarchar(150) NOT NULL,
    [SizeBytes] bigint NOT NULL,
    [CreatedDate] datetimeoffset NOT NULL,
    [UpdatedDate] datetimeoffset NULL,
    [CreatedUser] bigint NOT NULL,
    [UpdatedUser] bigint NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_ContractAttachment] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_ContractAttachment_Size] CHECK ([SizeBytes] > 0 AND [SizeBytes] <= 20971520),
    CONSTRAINT [FK_ContractAttachment_Contract_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [collection].[Contract] ([Id])
);

CREATE INDEX [IX_ContractAttachment_Contract_Deleted_Id] ON [collection].[ContractAttachment] ([ContractId], [IsDeleted], [Id] DESC);

CREATE UNIQUE INDEX [UX_ContractAttachment_StoredFileName] ON [collection].[ContractAttachment] ([StoredFileName]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260916202500_AddCollectionContractAttachment', N'9.0.5');

COMMIT;
GO

