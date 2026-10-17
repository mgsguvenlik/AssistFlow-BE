BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002210728_AddCollectionSmsNotifications'
)
BEGIN
    CREATE TABLE [collection].[SmsNotification] (
        [Id] bigint NOT NULL IDENTITY,
        [ContractId] bigint NOT NULL,
        [RatePeriodId] bigint NOT NULL,
        [OldAmount] decimal(18,2) NOT NULL,
        [NewAmount] decimal(18,2) NOT NULL,
        [IncreasePercent] decimal(28,8) NULL,
        [EffectiveDate] date NOT NULL,
        [Status] tinyint NOT NULL,
        [AttemptCount] int NOT NULL,
        [CreatedDate] datetimeoffset NOT NULL,
        [UpdatedDate] datetimeoffset NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_SmsNotification] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_SmsNotification_Attempts] CHECK ([AttemptCount] > 0),
        CONSTRAINT [CK_SmsNotification_Status] CHECK ([Status] BETWEEN 0 AND 5)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002210728_AddCollectionSmsNotifications'
)
BEGIN
    CREATE TABLE [collection].[SmsAttempt] (
        [Id] bigint NOT NULL IDENTITY,
        [NotificationId] bigint NOT NULL,
        [Sequence] int NOT NULL,
        [RecipientCustomerId] bigint NULL,
        [Phone] nvarchar(12) NOT NULL,
        [Message] nvarchar(2000) NOT NULL,
        [Template] nvarchar(2000) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [IsAutomatic] bit NOT NULL,
        [IsSimulation] bit NOT NULL,
        [Status] tinyint NOT NULL,
        [ResultMessage] nvarchar(500) NULL,
        [ErrorCode] nvarchar(100) NULL,
        [PackageId] nvarchar(100) NULL,
        [CreatedDate] datetimeoffset NOT NULL,
        [CreatedUser] bigint NOT NULL,
        [StartedDate] datetimeoffset NULL,
        [CompletedDate] datetimeoffset NULL,
        CONSTRAINT [PK_SmsAttempt] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_SmsAttempt_Status] CHECK ([Status] BETWEEN 0 AND 5),
        CONSTRAINT [FK_SmsAttempt_SmsNotification_NotificationId] FOREIGN KEY ([NotificationId]) REFERENCES [collection].[SmsNotification] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002210728_AddCollectionSmsNotifications'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SmsAttempt_NotificationId_Sequence] ON [collection].[SmsAttempt] ([NotificationId], [Sequence]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002210728_AddCollectionSmsNotifications'
)
BEGIN
    CREATE INDEX [IX_SmsAttempt_Status_CreatedDate_Id] ON [collection].[SmsAttempt] ([Status], [CreatedDate], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002210728_AddCollectionSmsNotifications'
)
BEGIN
    CREATE INDEX [IX_SmsNotification_ContractId_CreatedDate_Id] ON [collection].[SmsNotification] ([ContractId], [CreatedDate], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002210728_AddCollectionSmsNotifications'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SmsNotification_RatePeriodId] ON [collection].[SmsNotification] ([RatePeriodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002210728_AddCollectionSmsNotifications'
)
BEGIN
    CREATE INDEX [IX_SmsNotification_Status_UpdatedDate_Id] ON [collection].[SmsNotification] ([Status], [UpdatedDate], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002210728_AddCollectionSmsNotifications'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002210728_AddCollectionSmsNotifications', N'9.0.5');
END;

COMMIT;
GO

