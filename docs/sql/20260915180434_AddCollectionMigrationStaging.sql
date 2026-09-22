BEGIN TRANSACTION;
CREATE TABLE [collection].[MigrationBatch] (
    [Id] bigint NOT NULL IDENTITY,
    [SourceSystem] nvarchar(50) NOT NULL,
    [SnapshotKey] nvarchar(200) NOT NULL,
    [ManifestHash] binary(32) NOT NULL,
    [RuleVersion] nvarchar(50) NOT NULL,
    [NormalizationVersion] nvarchar(50) NOT NULL,
    [Status] tinyint NOT NULL,
    [CreatedDate] datetimeoffset NOT NULL,
    [CreatedUser] bigint NOT NULL,
    [CompletedDate] datetimeoffset NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_MigrationBatch] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_MigrationBatch_Hash] CHECK (DATALENGTH([ManifestHash]) = 32),
    CONSTRAINT [CK_MigrationBatch_Status] CHECK ([Status] BETWEEN 0 AND 6)
);

CREATE TABLE [collection].[MigrationMap] (
    [Id] bigint NOT NULL IDENTITY,
    [SourceSystem] nvarchar(50) NOT NULL,
    [EntityCode] nvarchar(40) NOT NULL,
    [SourceId] nvarchar(100) NOT NULL,
    [TargetContractId] bigint NULL,
    [TargetRatePeriodId] bigint NULL,
    [TargetPaymentId] bigint NULL,
    [AppliedPayloadHash] binary(32) NOT NULL,
    [FirstBatchId] bigint NOT NULL,
    [LastBatchId] bigint NOT NULL,
    [CreatedDate] datetimeoffset NOT NULL,
    [LastSeenDate] datetimeoffset NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_MigrationMap] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_MigrationMap_Hash] CHECK (DATALENGTH([AppliedPayloadHash]) = 32),
    CONSTRAINT [CK_MigrationMap_Target] CHECK ((CASE WHEN [TargetContractId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetRatePeriodId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetPaymentId] IS NULL THEN 0 ELSE 1 END) = 1),
    CONSTRAINT [FK_MigrationMap_ContractRatePeriod_TargetRatePeriodId] FOREIGN KEY ([TargetRatePeriodId]) REFERENCES [collection].[ContractRatePeriod] ([Id]),
    CONSTRAINT [FK_MigrationMap_Contract_TargetContractId] FOREIGN KEY ([TargetContractId]) REFERENCES [collection].[Contract] ([Id]),
    CONSTRAINT [FK_MigrationMap_MigrationBatch_FirstBatchId] FOREIGN KEY ([FirstBatchId]) REFERENCES [collection].[MigrationBatch] ([Id]),
    CONSTRAINT [FK_MigrationMap_MigrationBatch_LastBatchId] FOREIGN KEY ([LastBatchId]) REFERENCES [collection].[MigrationBatch] ([Id]),
    CONSTRAINT [FK_MigrationMap_Payment_TargetPaymentId] FOREIGN KEY ([TargetPaymentId]) REFERENCES [collection].[Payment] ([Id])
);

CREATE TABLE [collection].[MigrationReferenceMap] (
    [Id] bigint NOT NULL IDENTITY,
    [BatchId] bigint NOT NULL,
    [ReferenceKind] nvarchar(40) NOT NULL,
    [SourceId] nvarchar(100) NOT NULL,
    [TargetCustomerId] bigint NULL,
    [TargetServiceTypeId] bigint NULL,
    [TargetCurrencyTypeId] bigint NULL,
    [TargetPaymentFrequencyId] bigint NULL,
    [TargetPaymentMethodId] bigint NULL,
    [TargetSubscriptionStatusId] bigint NULL,
    [TargetContractStatusId] bigint NULL,
    [MatchMethod] nvarchar(40) NOT NULL,
    [Evidence] nvarchar(1000) NULL,
    [Status] tinyint NOT NULL,
    [DecidedUser] bigint NULL,
    [DecidedDate] datetimeoffset NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_MigrationReferenceMap] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_MigrationReferenceMap_Status] CHECK ([Status] BETWEEN 0 AND 2),
    CONSTRAINT [CK_MigrationReferenceMap_Target] CHECK ([Status] <> 1 OR (CASE WHEN [TargetCustomerId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetServiceTypeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetCurrencyTypeId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetPaymentFrequencyId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetPaymentMethodId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetSubscriptionStatusId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TargetContractStatusId] IS NULL THEN 0 ELSE 1 END) = 1),
    CONSTRAINT [FK_MigrationReferenceMap_ContractStatus_TargetContractStatusId] FOREIGN KEY ([TargetContractStatusId]) REFERENCES [collection].[ContractStatus] ([Id]),
    CONSTRAINT [FK_MigrationReferenceMap_CurrencyType_TargetCurrencyTypeId] FOREIGN KEY ([TargetCurrencyTypeId]) REFERENCES [CurrencyType] ([Id]),
    CONSTRAINT [FK_MigrationReferenceMap_Customers_TargetCustomerId] FOREIGN KEY ([TargetCustomerId]) REFERENCES [Customers] ([Id]),
    CONSTRAINT [FK_MigrationReferenceMap_MigrationBatch_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [collection].[MigrationBatch] ([Id]),
    CONSTRAINT [FK_MigrationReferenceMap_PaymentFrequency_TargetPaymentFrequencyId] FOREIGN KEY ([TargetPaymentFrequencyId]) REFERENCES [collection].[PaymentFrequency] ([Id]),
    CONSTRAINT [FK_MigrationReferenceMap_PaymentMethod_TargetPaymentMethodId] FOREIGN KEY ([TargetPaymentMethodId]) REFERENCES [collection].[PaymentMethod] ([Id]),
    CONSTRAINT [FK_MigrationReferenceMap_ServiceType_TargetServiceTypeId] FOREIGN KEY ([TargetServiceTypeId]) REFERENCES [ServiceType] ([Id]),
    CONSTRAINT [FK_MigrationReferenceMap_SubscriptionStatus_TargetSubscriptionStatusId] FOREIGN KEY ([TargetSubscriptionStatusId]) REFERENCES [collection].[SubscriptionStatus] ([Id])
);

CREATE TABLE [collection].[MigrationSourceRow] (
    [Id] bigint NOT NULL IDENTITY,
    [BatchId] bigint NOT NULL,
    [EntityCode] nvarchar(40) NOT NULL,
    [SourceId] nvarchar(100) NOT NULL,
    [SourceParentId] nvarchar(100) NULL,
    [Payload] nvarchar(max) NOT NULL,
    [PayloadHash] binary(32) NOT NULL,
    [StagedDate] datetimeoffset NOT NULL,
    CONSTRAINT [PK_MigrationSourceRow] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_MigrationSourceRow_Hash] CHECK (DATALENGTH([PayloadHash]) = 32),
    CONSTRAINT [CK_MigrationSourceRow_Payload] CHECK (ISJSON([Payload]) = 1),
    CONSTRAINT [FK_MigrationSourceRow_MigrationBatch_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [collection].[MigrationBatch] ([Id])
);

CREATE TABLE [collection].[MigrationContractStage] (
    [SourceRowId] bigint NOT NULL,
    [SourceCustomerId] nvarchar(100) NULL,
    [SubscriberNoRaw] nvarchar(200) NULL,
    [SubscriberNoNormalized] nvarchar(200) NULL,
    [SourceServiceTypeId] nvarchar(100) NULL,
    [SourceContractStatusId] nvarchar(100) NULL,
    [SourceSubscriptionStatusId] nvarchar(100) NULL,
    [SourcePaymentMethodId] nvarchar(100) NULL,
    [StartingMonth] smallint NULL,
    [StartingYear] smallint NULL,
    [EndDate] date NULL,
    [GtsNo] nvarchar(50) NULL,
    [IvrNo] nvarchar(50) NULL,
    [AttachmentName] nvarchar(255) NULL,
    [AttachmentPath] nvarchar(1000) NULL,
    [TargetCustomerId] bigint NULL,
    [TargetServiceTypeId] bigint NULL,
    [Status] tinyint NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_MigrationContractStage] PRIMARY KEY ([SourceRowId]),
    CONSTRAINT [CK_MigrationContractStage_Status] CHECK ([Status] BETWEEN 0 AND 5),
    CONSTRAINT [FK_MigrationContractStage_Customers_TargetCustomerId] FOREIGN KEY ([TargetCustomerId]) REFERENCES [Customers] ([Id]),
    CONSTRAINT [FK_MigrationContractStage_MigrationSourceRow_SourceRowId] FOREIGN KEY ([SourceRowId]) REFERENCES [collection].[MigrationSourceRow] ([Id]),
    CONSTRAINT [FK_MigrationContractStage_ServiceType_TargetServiceTypeId] FOREIGN KEY ([TargetServiceTypeId]) REFERENCES [ServiceType] ([Id])
);

CREATE TABLE [collection].[MigrationIssue] (
    [Id] bigint NOT NULL IDENTITY,
    [SourceRowId] bigint NOT NULL,
    [IssueCode] nvarchar(60) NOT NULL,
    [Severity] tinyint NOT NULL,
    [Status] tinyint NOT NULL,
    [Details] nvarchar(2000) NULL,
    [ResolutionNote] nvarchar(2000) NULL,
    [RuleVersion] nvarchar(50) NOT NULL,
    [CreatedDate] datetimeoffset NOT NULL,
    [ResolvedUser] bigint NULL,
    [ResolvedDate] datetimeoffset NULL,
    CONSTRAINT [PK_MigrationIssue] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_MigrationIssue_Severity] CHECK ([Severity] IN (0,1)),
    CONSTRAINT [CK_MigrationIssue_Status] CHECK ([Status] BETWEEN 0 AND 2),
    CONSTRAINT [FK_MigrationIssue_MigrationSourceRow_SourceRowId] FOREIGN KEY ([SourceRowId]) REFERENCES [collection].[MigrationSourceRow] ([Id])
);

CREATE TABLE [collection].[MigrationRatePeriodStage] (
    [SourceRowId] bigint NOT NULL,
    [SourceContractId] nvarchar(100) NULL,
    [SourceCustomerId] nvarchar(100) NULL,
    [EffectiveFrom] date NULL,
    [EffectiveToExclusive] date NULL,
    [Amount] decimal(18,2) NULL,
    [SourceCurrencyId] nvarchar(100) NULL,
    [SourcePaymentTypeId] nvarchar(100) NULL,
    [ProcessType] nvarchar(100) NULL,
    [Description] nvarchar(1000) NULL,
    [TargetCurrencyTypeId] bigint NULL,
    [TargetPaymentFrequencyId] bigint NULL,
    [BillingBehavior] tinyint NULL,
    [Status] tinyint NOT NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_MigrationRatePeriodStage] PRIMARY KEY ([SourceRowId]),
    CONSTRAINT [CK_MigrationRateStage_Behavior] CHECK ([BillingBehavior] IS NULL OR [BillingBehavior] IN (0,1,2)),
    CONSTRAINT [CK_MigrationRateStage_Status] CHECK ([Status] BETWEEN 0 AND 5),
    CONSTRAINT [FK_MigrationRatePeriodStage_CurrencyType_TargetCurrencyTypeId] FOREIGN KEY ([TargetCurrencyTypeId]) REFERENCES [CurrencyType] ([Id]),
    CONSTRAINT [FK_MigrationRatePeriodStage_MigrationSourceRow_SourceRowId] FOREIGN KEY ([SourceRowId]) REFERENCES [collection].[MigrationSourceRow] ([Id]),
    CONSTRAINT [FK_MigrationRatePeriodStage_PaymentFrequency_TargetPaymentFrequencyId] FOREIGN KEY ([TargetPaymentFrequencyId]) REFERENCES [collection].[PaymentFrequency] ([Id])
);

CREATE UNIQUE INDEX [UX_MigrationBatch_Source_Snapshot] ON [collection].[MigrationBatch] ([SourceSystem], [SnapshotKey]);

CREATE INDEX [IX_MigrationContractStage_Status_Row] ON [collection].[MigrationContractStage] ([Status], [SourceRowId]);

CREATE INDEX [IX_MigrationContractStage_Subscriber] ON [collection].[MigrationContractStage] ([SubscriberNoNormalized]);

CREATE INDEX [IX_MigrationContractStage_TargetCustomerId] ON [collection].[MigrationContractStage] ([TargetCustomerId]);

CREATE INDEX [IX_MigrationContractStage_TargetServiceTypeId] ON [collection].[MigrationContractStage] ([TargetServiceTypeId]);

CREATE INDEX [IX_MigrationIssue_Status_Code_Row_Id] ON [collection].[MigrationIssue] ([Status], [IssueCode], [SourceRowId], [Id]);

CREATE UNIQUE INDEX [UX_MigrationIssue_Open_Row_Code] ON [collection].[MigrationIssue] ([SourceRowId], [IssueCode]) WHERE [Status] = 0;

CREATE INDEX [IX_MigrationMap_FirstBatchId] ON [collection].[MigrationMap] ([FirstBatchId]);

CREATE INDEX [IX_MigrationMap_LastBatchId] ON [collection].[MigrationMap] ([LastBatchId]);

CREATE UNIQUE INDEX [UX_MigrationMap_Source_Entity_Id] ON [collection].[MigrationMap] ([SourceSystem], [EntityCode], [SourceId]);

CREATE UNIQUE INDEX [UX_MigrationMap_TargetContract] ON [collection].[MigrationMap] ([TargetContractId]) WHERE [TargetContractId] IS NOT NULL;

CREATE UNIQUE INDEX [UX_MigrationMap_TargetPayment] ON [collection].[MigrationMap] ([TargetPaymentId]) WHERE [TargetPaymentId] IS NOT NULL;

CREATE UNIQUE INDEX [UX_MigrationMap_TargetRate] ON [collection].[MigrationMap] ([TargetRatePeriodId]) WHERE [TargetRatePeriodId] IS NOT NULL;

CREATE INDEX [IX_MigrationRatePeriodStage_TargetCurrencyTypeId] ON [collection].[MigrationRatePeriodStage] ([TargetCurrencyTypeId]);

CREATE INDEX [IX_MigrationRatePeriodStage_TargetPaymentFrequencyId] ON [collection].[MigrationRatePeriodStage] ([TargetPaymentFrequencyId]);

CREATE INDEX [IX_MigrationRateStage_Contract_Date_Row] ON [collection].[MigrationRatePeriodStage] ([SourceContractId], [EffectiveFrom], [SourceRowId]);

CREATE INDEX [IX_MigrationRateStage_Status_Row] ON [collection].[MigrationRatePeriodStage] ([Status], [SourceRowId]);

CREATE INDEX [IX_MigrationReferenceMap_Status_Kind_Id] ON [collection].[MigrationReferenceMap] ([Status], [ReferenceKind], [Id]);

CREATE INDEX [IX_MigrationReferenceMap_TargetContractStatusId] ON [collection].[MigrationReferenceMap] ([TargetContractStatusId]);

CREATE INDEX [IX_MigrationReferenceMap_TargetCurrencyTypeId] ON [collection].[MigrationReferenceMap] ([TargetCurrencyTypeId]);

CREATE INDEX [IX_MigrationReferenceMap_TargetCustomerId] ON [collection].[MigrationReferenceMap] ([TargetCustomerId]);

CREATE INDEX [IX_MigrationReferenceMap_TargetPaymentFrequencyId] ON [collection].[MigrationReferenceMap] ([TargetPaymentFrequencyId]);

CREATE INDEX [IX_MigrationReferenceMap_TargetPaymentMethodId] ON [collection].[MigrationReferenceMap] ([TargetPaymentMethodId]);

CREATE INDEX [IX_MigrationReferenceMap_TargetServiceTypeId] ON [collection].[MigrationReferenceMap] ([TargetServiceTypeId]);

CREATE INDEX [IX_MigrationReferenceMap_TargetSubscriptionStatusId] ON [collection].[MigrationReferenceMap] ([TargetSubscriptionStatusId]);

CREATE UNIQUE INDEX [UX_MigrationReferenceMap_Batch_Kind_Source] ON [collection].[MigrationReferenceMap] ([BatchId], [ReferenceKind], [SourceId]);

CREATE INDEX [IX_MigrationSourceRow_Batch_Entity_Id] ON [collection].[MigrationSourceRow] ([BatchId], [EntityCode], [Id]);

CREATE INDEX [IX_MigrationSourceRow_Batch_Parent_Id] ON [collection].[MigrationSourceRow] ([BatchId], [EntityCode], [SourceParentId], [Id]);

CREATE UNIQUE INDEX [UX_MigrationSourceRow_Batch_Entity_Source] ON [collection].[MigrationSourceRow] ([BatchId], [EntityCode], [SourceId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260915180434_AddCollectionMigrationStaging', N'9.0.5');

COMMIT;
GO

