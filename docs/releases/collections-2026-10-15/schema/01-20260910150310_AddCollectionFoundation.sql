-- İNCELEME PAKETİ: Yedek/prova ve onay olmadan çalıştırmayın.
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AssistFlow' THROW 51000, N'Yanlış hedef DB.', 1;
GO
IF ISNULL(OBJECT_ID(N'__EFMigrationsHistory'),-1)<>OBJECT_ID(N'dbo.__EFMigrationsHistory') THROW 51000, N'Migration varsayılan şeması uyuşmuyor.', 1;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260905164011_AddEkbTenantModule') THROW 51000, N'Önceki migration eksik.', 1;
GO
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    IF SCHEMA_ID(N'collection') IS NULL EXEC(N'CREATE SCHEMA [collection];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE TABLE [collection].[ContractStatus] (
        [Id] bigint NOT NULL IDENTITY,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ContractStatus] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE TABLE [collection].[GroupStatus] (
        [Id] bigint NOT NULL IDENTITY,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_GroupStatus] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE TABLE [collection].[PaymentFrequency] (
        [Id] bigint NOT NULL IDENTITY,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [IntervalMonths] smallint NOT NULL,
        [DisplayOrder] smallint NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_PaymentFrequency] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PaymentFrequency_IntervalMonths] CHECK ([IntervalMonths] IN (1,2,3,4,6,12,24,36))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE TABLE [collection].[PaymentMethod] (
        [Id] bigint NOT NULL IDENTITY,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_PaymentMethod] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE TABLE [collection].[PaymentOperation] (
        [Id] bigint NOT NULL IDENTITY,
        [RequestId] uniqueidentifier NOT NULL,
        [ActorUserId] bigint NOT NULL,
        [Kind] tinyint NOT NULL,
        [PayloadHash] varbinary(32) NOT NULL,
        [PaymentId] bigint NOT NULL,
        [BeforeJson] nvarchar(max) NULL,
        [AfterJson] nvarchar(max) NULL,
        [CompletedDate] datetimeoffset NOT NULL,
        CONSTRAINT [PK_PaymentOperation] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PaymentOperation_After] CHECK ([AfterJson] IS NULL OR ISJSON([AfterJson]) = 1),
        CONSTRAINT [CK_PaymentOperation_Before] CHECK ([BeforeJson] IS NULL OR ISJSON([BeforeJson]) = 1),
        CONSTRAINT [CK_PaymentOperation_Hash] CHECK (DATALENGTH([PayloadHash]) = 32),
        CONSTRAINT [CK_PaymentOperation_Kind] CHECK ([Kind] IN (0,1,2)),
        CONSTRAINT [CK_PaymentOperation_Snapshots] CHECK (([Kind] = 0 AND [BeforeJson] IS NULL AND [AfterJson] IS NOT NULL) OR ([Kind] = 1 AND [BeforeJson] IS NOT NULL AND [AfterJson] IS NOT NULL) OR ([Kind] = 2 AND [BeforeJson] IS NOT NULL AND [AfterJson] IS NULL))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE TABLE [collection].[SubscriptionStatus] (
        [Id] bigint NOT NULL IDENTITY,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_SubscriptionStatus] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE TABLE [collection].[Contract] (
        [Id] bigint NOT NULL IDENTITY,
        [CustomerId] bigint NOT NULL,
        [ServiceTypeId] bigint NOT NULL,
        [StartDate] date NOT NULL,
        [EndDate] date NULL,
        [GtsNo] nvarchar(50) NULL,
        [IvrNo] nvarchar(50) NULL,
        [SubscriptionStatusId] bigint NULL,
        [ContractStatusId] bigint NULL,
        [PaymentMethodId] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedDate] datetimeoffset NOT NULL,
        [UpdatedDate] datetimeoffset NULL,
        [CreatedUser] bigint NOT NULL,
        [UpdatedUser] bigint NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Contract] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Contract_DateRange] CHECK ([EndDate] IS NULL OR [EndDate] >= [StartDate]),
        CONSTRAINT [FK_Contract_ContractStatus_ContractStatusId] FOREIGN KEY ([ContractStatusId]) REFERENCES [collection].[ContractStatus] ([Id]),
        CONSTRAINT [FK_Contract_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]),
        CONSTRAINT [FK_Contract_PaymentMethod_PaymentMethodId] FOREIGN KEY ([PaymentMethodId]) REFERENCES [collection].[PaymentMethod] ([Id]),
        CONSTRAINT [FK_Contract_ServiceType_ServiceTypeId] FOREIGN KEY ([ServiceTypeId]) REFERENCES [ServiceType] ([Id]),
        CONSTRAINT [FK_Contract_SubscriptionStatus_SubscriptionStatusId] FOREIGN KEY ([SubscriptionStatusId]) REFERENCES [collection].[SubscriptionStatus] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE TABLE [collection].[ContractPeriodFollowUp] (
        [Id] bigint NOT NULL IDENTITY,
        [ContractId] bigint NOT NULL,
        [Period] date NOT NULL,
        [GroupStatusId] bigint NULL,
        [Description] nvarchar(500) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedDate] datetimeoffset NOT NULL,
        [UpdatedDate] datetimeoffset NULL,
        [CreatedUser] bigint NOT NULL,
        [UpdatedUser] bigint NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_ContractPeriodFollowUp] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ContractPeriodFollowUp_Period] CHECK (DAY([Period]) = 1),
        CONSTRAINT [FK_ContractPeriodFollowUp_Contract_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [collection].[Contract] ([Id]),
        CONSTRAINT [FK_ContractPeriodFollowUp_GroupStatus_GroupStatusId] FOREIGN KEY ([GroupStatusId]) REFERENCES [collection].[GroupStatus] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE TABLE [collection].[ContractRatePeriod] (
        [Id] bigint NOT NULL IDENTITY,
        [ContractId] bigint NOT NULL,
        [EffectiveFrom] date NOT NULL,
        [EffectiveToExclusive] date NULL,
        [BillingAnchor] date NOT NULL,
        [PaymentFrequencyId] bigint NOT NULL,
        [Amount] decimal(18,2) NULL,
        [CurrencyTypeId] bigint NULL,
        [BillingBehavior] tinyint NOT NULL,
        [ChangeReason] nvarchar(100) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedDate] datetimeoffset NOT NULL,
        [UpdatedDate] datetimeoffset NULL,
        [CreatedUser] bigint NOT NULL,
        [UpdatedUser] bigint NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_ContractRatePeriod] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ContractRatePeriod_Amount] CHECK ([Amount] IS NULL OR [Amount] >= 0),
        CONSTRAINT [CK_ContractRatePeriod_Anchor] CHECK ([BillingAnchor] <= [EffectiveFrom]),
        CONSTRAINT [CK_ContractRatePeriod_Behavior] CHECK ([BillingBehavior] IN (0,1,2)),
        CONSTRAINT [CK_ContractRatePeriod_Billable] CHECK ([BillingBehavior] <> 0 OR ([Amount] IS NOT NULL AND [CurrencyTypeId] IS NOT NULL)),
        CONSTRAINT [CK_ContractRatePeriod_Dates] CHECK ([EffectiveToExclusive] IS NULL OR [EffectiveToExclusive] > [EffectiveFrom]),
        CONSTRAINT [FK_ContractRatePeriod_Contract_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [collection].[Contract] ([Id]),
        CONSTRAINT [FK_ContractRatePeriod_CurrencyType_CurrencyTypeId] FOREIGN KEY ([CurrencyTypeId]) REFERENCES [CurrencyType] ([Id]),
        CONSTRAINT [FK_ContractRatePeriod_PaymentFrequency_PaymentFrequencyId] FOREIGN KEY ([PaymentFrequencyId]) REFERENCES [collection].[PaymentFrequency] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE TABLE [collection].[Payment] (
        [Id] bigint NOT NULL IDENTITY,
        [ContractId] bigint NOT NULL,
        [Period] date NOT NULL,
        [PaymentDate] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [CurrencyTypeId] bigint NOT NULL,
        [Description] nvarchar(1000) NULL,
        [IsFree] bit NOT NULL,
        [CreatedDate] datetimeoffset NOT NULL,
        [UpdatedDate] datetimeoffset NULL,
        [CreatedUser] bigint NOT NULL,
        [UpdatedUser] bigint NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Payment] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Payment_Period] CHECK (DAY([Period]) = 1),
        CONSTRAINT [FK_Payment_Contract_ContractId] FOREIGN KEY ([ContractId]) REFERENCES [collection].[Contract] ([Id]),
        CONSTRAINT [FK_Payment_CurrencyType_CurrencyTypeId] FOREIGN KEY ([CurrencyTypeId]) REFERENCES [CurrencyType] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE INDEX [IX_Contract_ContractStatusId] ON [collection].[Contract] ([ContractStatusId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE INDEX [IX_Contract_Customer_Deleted_Id] ON [collection].[Contract] ([CustomerId], [IsDeleted], [Id] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE INDEX [IX_Contract_PaymentMethodId] ON [collection].[Contract] ([PaymentMethodId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE INDEX [IX_Contract_ServiceType_Deleted_Id] ON [collection].[Contract] ([ServiceTypeId], [IsDeleted], [Id] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE INDEX [IX_Contract_SubscriptionStatusId] ON [collection].[Contract] ([SubscriptionStatusId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE INDEX [IX_ContractPeriodFollowUp_GroupStatusId] ON [collection].[ContractPeriodFollowUp] ([GroupStatusId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ContractPeriodFollowUp_Contract_Period] ON [collection].[ContractPeriodFollowUp] ([ContractId], [Period]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE INDEX [IX_ContractRatePeriod_CurrencyTypeId] ON [collection].[ContractRatePeriod] ([CurrencyTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE INDEX [IX_ContractRatePeriod_PaymentFrequencyId] ON [collection].[ContractRatePeriod] ([PaymentFrequencyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ContractRatePeriod_Open] ON [collection].[ContractRatePeriod] ([ContractId]) WHERE [EffectiveToExclusive] IS NULL AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ContractRatePeriod_Start] ON [collection].[ContractRatePeriod] ([ContractId], [EffectiveFrom]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [UX_ContractStatus_Code] ON [collection].[ContractStatus] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [UX_GroupStatus_Code] ON [collection].[GroupStatus] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE INDEX [IX_Payment_Contract_Period_Currency_Id] ON [collection].[Payment] ([ContractId], [Period], [CurrencyTypeId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE INDEX [IX_Payment_CurrencyTypeId] ON [collection].[Payment] ([CurrencyTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PaymentFrequency_Code] ON [collection].[PaymentFrequency] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PaymentFrequency_IntervalMonths] ON [collection].[PaymentFrequency] ([IntervalMonths]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PaymentMethod_Code] ON [collection].[PaymentMethod] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE INDEX [IX_PaymentOperation_Payment_Date_Id] ON [collection].[PaymentOperation] ([PaymentId], [CompletedDate], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [UX_PaymentOperation_RequestId] ON [collection].[PaymentOperation] ([RequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    CREATE UNIQUE INDEX [UX_SubscriptionStatus_Code] ON [collection].[SubscriptionStatus] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910150310_AddCollectionFoundation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260910150310_AddCollectionFoundation', N'9.0.5');
END;

COMMIT;
GO

