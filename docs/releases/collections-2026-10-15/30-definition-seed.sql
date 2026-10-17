-- Hazırlık scripti; canlıya uygulanmadı. Şema kurulumu ve prova sonrasında açık onayla @Apply=1 yapılır.
DECLARE @Apply bit=0;
IF DB_NAME()<>N'AssistFlow' THROW 51000,N'Hedef AssistFlow olmalıdır.',1;
IF @Apply<>1 THROW 51000,N'Uygulama kapalı. Önce yayın planı ve yedek/prova onaylanmalıdır.',1;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'CollectionDefinitionSeed',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,N'Tanım kilidi alınamadı.',1;
 DECLARE @Frequency TABLE(Code nvarchar(50),Name nvarchar(150),Months smallint);
 INSERT @Frequency VALUES(N'MONTHLY',N'Aylık',1),(N'EVERY_2_MONTHS',N'2 Aylık',2),
 (N'EVERY_3_MONTHS',N'3 Aylık',3),(N'EVERY_4_MONTHS',N'4 Aylık',4),
 (N'EVERY_6_MONTHS',N'6 Aylık',6),(N'YEARLY',N'Yıllık',12),
 (N'EVERY_24_MONTHS',N'2 Yıllık',24),(N'EVERY_36_MONTHS',N'3 Yıllık',36);
 IF EXISTS(SELECT 1 FROM @Frequency s JOIN collection.PaymentFrequency t ON t.Code=s.Code WHERE t.IntervalMonths<>s.Months)
    THROW 51000,N'Ödeme dönemi kod/ay karşılığı uyuşmuyor.',1;
 IF EXISTS(SELECT 1 FROM @Frequency s JOIN collection.PaymentFrequency t ON t.IntervalMonths=s.Months OR t.Name=s.Name WHERE t.Code<>s.Code)
    THROW 51000,N'Ödeme dönemi ad/ay çakışması var.',1;
 INSERT collection.PaymentFrequency(Code,Name,IntervalMonths,DisplayOrder,IsActive)
 SELECT Code,Name,Months,Months,1 FROM @Frequency s WHERE NOT EXISTS(SELECT 1 FROM collection.PaymentFrequency t WHERE t.Code=s.Code);
 DECLARE @Status TABLE(Code nvarchar(50),Name nvarchar(150));
 INSERT @Status VALUES(N'EXISTS',N'VAR'),(N'NONE',N'YOK'),(N'UNKNOWN',N'Belirtilmemiş');
 IF EXISTS(SELECT 1 FROM @Status s JOIN collection.ContractStatus t ON t.Name=s.Name WHERE t.Code<>s.Code)
    THROW 51000,N'Sözleşme durumu ad/kod çakışması var.',1;
 INSERT collection.ContractStatus(Code,Name,IsActive)
 SELECT Code,Name,1 FROM @Status s WHERE NOT EXISTS(SELECT 1 FROM collection.ContractStatus t WHERE t.Code=s.Code);
 DECLARE @Subscription TABLE(Code nvarchar(50),Name nvarchar(150));
 INSERT @Subscription VALUES(N'ACTIVE',N'Aktif'),(N'FROZEN',N'Donuk');
 IF EXISTS(SELECT 1 FROM @Subscription s JOIN collection.SubscriptionStatus t ON t.Name=s.Name WHERE t.Code<>s.Code)
    THROW 51000,N'Abonelik durumu ad/kod çakışması var.',1;
 INSERT collection.SubscriptionStatus(Code,Name,IsActive)
 SELECT Code,Name,1 FROM @Subscription s WHERE NOT EXISTS(SELECT 1 FROM collection.SubscriptionStatus t WHERE t.Code=s.Code);
 DECLARE @Group TABLE(Code nvarchar(50),Name nvarchar(150));
 INSERT @Group VALUES(N'OFFSET',N'Mahsup'),(N'CANCELLED',N'İptal'),(N'PENDING',N'Beklemede'),
 (N'FREE',N'Ücretsiz'),(N'INVOICED',N'Fatura Kesildi'),(N'PAID',N'Ödendi'),(N'APPROVED',N'Onaylandı');
 IF EXISTS(SELECT 1 FROM @Group s JOIN collection.GroupStatus t ON t.Name=s.Name WHERE t.Code<>s.Code)
    THROW 51000,N'Grup durumu ad/kod çakışması var.',1;
 INSERT collection.GroupStatus(Code,Name,IsActive)
 SELECT Code,Name,1 FROM @Group s WHERE NOT EXISTS(SELECT 1 FROM collection.GroupStatus t WHERE t.Code=s.Code);
 DECLARE @Method TABLE(Code nvarchar(50),Name nvarchar(150));
 INSERT @Method VALUES(N'FINANSBANK_POS',N'Finansbank POS'),(N'GTS',N'GTS'),(N'IVR',N'IVR'),(N'WEB',N'Web'),
 (N'MAIL_ORDER',N'Mail Order'),(N'OFFSET',N'Mahsup'),(N'CASH_MANUAL',N'Nakit / Manuel'),
 (N'LEGACY_FREE',N'Ücretsiz'),(N'ISBANK_POS',N'İş Bankası POS'),(N'BANK_TRANSFER',N'Banka Havalesi');
 IF EXISTS(SELECT 1 FROM @Method s JOIN collection.PaymentMethod t ON t.Name=s.Name WHERE t.Code<>s.Code)
    THROW 51000,N'Ödeme yöntemi ad/kod çakışması var.',1;
 INSERT collection.PaymentMethod(Code,Name,IsActive)
 SELECT Code,Name,CASE WHEN Code IN(N'GTS',N'BANK_TRANSFER') THEN 1 ELSE 0 END
 FROM @Method s WHERE NOT EXISTS(SELECT 1 FROM collection.PaymentMethod t WHERE t.Code=s.Code);
 COMMIT;
 SELECT N'PaymentFrequency' AS Definition,Code,Name,IsActive FROM collection.PaymentFrequency
 UNION ALL SELECT N'ContractStatus',Code,Name,IsActive FROM collection.ContractStatus
 UNION ALL SELECT N'SubscriptionStatus',Code,Name,IsActive FROM collection.SubscriptionStatus
 UNION ALL SELECT N'GroupStatus',Code,Name,IsActive FROM collection.GroupStatus
 UNION ALL SELECT N'PaymentMethod',Code,Name,IsActive FROM collection.PaymentMethod;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
