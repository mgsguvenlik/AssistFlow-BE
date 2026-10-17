-- Salt okunur; yalnız şema kurulumu ve veri uygulaması sonrasında kullanılır.
SET NOCOUNT ON;
SET LOCK_TIMEOUT 5000;
IF DB_NAME()<>N'AssistFlow' THROW 51000,N'Hedef AssistFlow olmalıdır.',1;
DECLARE @Expected TABLE(MigrationId nvarchar(150));
INSERT @Expected VALUES
(N'20260910150310_AddCollectionFoundation'),(N'20260912184617_AddCollectionOriginalAnchorDay'),
(N'20260913081707_AddCollectionContractCreationRequest'),(N'20260915180434_AddCollectionMigrationStaging'),
(N'20260916195834_AddCollectionMigrationCustomerTypeMap'),(N'20260916202500_AddCollectionContractAttachment'),
(N'20260927123000_AddCollectionTrackingIndexes'),(N'20260927160000_AddCollectionGroupParent'),
(N'20260927180000_AddCollectionCustomerNotes'),(N'20260927190000_AddCollectionGroupFollowUpSource'),
(N'20260929080736_AddCollectionInvoices'),(N'20260929084242_AddCollectionInvoiceOperations'),
(N'20260929100739_AddCollectionInvoiceLoads'),(N'20260929101445_AddCollectionInvoiceLoadRowAudit'),
(N'20260929111421_AddCollectionBankLoads'),(N'20260929112405_AddCollectionBankReferenceIndexes'),
(N'20260929130125_AddCollectionContractCorrections'),(N'20260930194336_AddCollectionCustomerAttachments'),
(N'20260930200005_AddCollectionCustomerFileActions'),(N'20260930230000_EnableCollectionRequestedPaymentMethods'),
(N'20261002210728_AddCollectionSmsNotifications');
IF EXISTS(SELECT 1 FROM @Expected e WHERE NOT EXISTS(SELECT 1 FROM dbo.__EFMigrationsHistory h WHERE h.MigrationId=e.MigrationId))
    THROW 51000,N'Beklenen Tahsilat migrationları eksik.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE schema_id=SCHEMA_ID(N'collection') AND (is_disabled=1 OR is_not_trusted=1))
    THROW 51000,N'Tahsilat ilişkileri devre dışı veya güvenilir değil.',1;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE schema_id=SCHEMA_ID(N'collection') AND (is_disabled=1 OR is_not_trusted=1))
    THROW 51000,N'Tahsilat CHECK kısıtları güvenilir değil.',1;
SELECT t.name AS TableName,SUM(p.rows) AS ApproximateRows FROM sys.tables t
 JOIN sys.partitions p ON p.object_id=t.object_id AND p.index_id IN(0,1)
 WHERE t.schema_id=SCHEMA_ID(N'collection') GROUP BY t.name ORDER BY t.name;
SELECT CurrencyTypeId,COUNT_BIG(*) AS RateCount,SUM(Amount) AS RateAmount
 FROM collection.ContractRatePeriod WHERE IsDeleted=0 GROUP BY CurrencyTypeId;
SELECT CurrencyTypeId,COUNT_BIG(*) AS PaymentCount,SUM(Amount) AS PaymentAmount
 FROM collection.Payment GROUP BY CurrencyTypeId;
-- Bu sorgular borç motorunun dönem hesabı yerine geçmez. Aynı filtreyle API toplamları ayrıca karşılaştırılır.
SELECT Status,COUNT_BIG(*) AS NotificationCount FROM collection.SmsNotification GROUP BY Status;
SELECT m.Name,mr.RoleId,r.Code AS RoleCode,mr.HasView,mr.HasEdit FROM dbo.Menus m
 LEFT JOIN dbo.MenuRole mr ON mr.ModulId=m.Id LEFT JOIN dbo.Roles r ON r.Id=mr.RoleId
 WHERE m.Name=N'CollectionFollowUp';
