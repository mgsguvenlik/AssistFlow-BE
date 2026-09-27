-- tools/Set-CollectionCustomerClassification.ps1 tarafından parametreli çalıştırılır.
-- @Apply=0 önizlemedir. İşlem yalnız AssistFlowTest üzerinde yetkilidir.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AssistFlowTest' THROW 51000, N'Bu işlem yalnız AssistFlowTest üzerinde çalışır.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'CollectionCustomerClassification',
        @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
    IF @LockResult < 0 THROW 51000, N'Sınıflandırma kilidi alınamadı.', 1;

    DECLARE @Rules table (Code nvarchar(50) COLLATE Turkish_CI_AS PRIMARY KEY, Kind nvarchar(20));
    INSERT @Rules SELECT Code,Kind FROM OPENJSON(@RulesJson)
        WITH (Code nvarchar(50),Kind nvarchar(20));
    IF EXISTS (SELECT 1 FROM @Rules WHERE Code LIKE N'FIN%' OR Code LIKE N'YKB%' OR Code=N'EMK')
        THROW 51000, N'Hakediş kodu sınıflandırma listesine eklenemez.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.CustomerType WITH (UPDLOCK,HOLDLOCK) WHERE Id=@IndividualId AND Code IN (N'N',N'BRYSL01'))
        OR NOT EXISTS (SELECT 1 FROM dbo.CustomerType WITH (UPDLOCK,HOLDLOCK) WHERE Id=@GroupId AND Code IN (N'GM',N'GRP01'))
        OR NOT EXISTS (SELECT 1 FROM dbo.CustomerType WITH (UPDLOCK,HOLDLOCK) WHERE Id=6 AND Code=N'BNK01')
        THROW 51000, N'Müşteri tipi tanımları önizlemeden sonra değişti.', 1;

    DECLARE @CurrentDefinition nvarchar(max)=OBJECT_DEFINITION(OBJECT_ID(N'stg.sp_SyncCustomers'));
    IF @CurrentDefinition IS NULL OR HASHBYTES('SHA2_256',@CurrentDefinition)<>HASHBYTES('SHA2_256',@OldDefinition)
        THROW 51000, N'Senkronizasyon tanımı değişti; yeniden önizleme gereklidir.', 1;

    DECLARE @CurrentCustomers nvarchar(max)=(SELECT c.Id,c.CustomerTypeId,c.CustomerGroupId,c.TenantId,
        UPPER(LTRIM(RTRIM(g.Code))) AS Code,
        CASE WHEN r.Kind=N'Individual' THEN @IndividualId ELSE @GroupId END AS TargetTypeId
        FROM dbo.Customers c WITH (UPDLOCK,HOLDLOCK)
        JOIN dbo.CustomerGroups g WITH (HOLDLOCK) ON g.Id=c.CustomerGroupId
        JOIN @Rules r ON r.Code=UPPER(LTRIM(RTRIM(g.Code))) COLLATE Turkish_CI_AS
        WHERE c.IsDeleted=0 AND c.CustomerTypeId IN (@IndividualId,@GroupId)
        ORDER BY c.Id FOR JSON PATH,INCLUDE_NULL_VALUES);
    IF HASHBYTES('SHA2_256',@CurrentCustomers)<>HASHBYTES('SHA2_256',@ExpectedCustomers)
        THROW 51000, N'Müşteri listesi önizlemeden sonra değişti; yeniden önizleme gereklidir.', 1;

    IF @Apply=1
    BEGIN
        DECLARE @Before table (Id bigint PRIMARY KEY,CustomerTypeId bigint NULL,CustomerGroupId bigint NULL,TenantId bigint NULL);
        INSERT @Before SELECT Id,CustomerTypeId,CustomerGroupId,TenantId FROM dbo.Customers WITH (UPDLOCK,HOLDLOCK);
        DECLARE @Expected table (Id bigint PRIMARY KEY,TargetTypeId bigint NOT NULL);
        INSERT @Expected SELECT Id,TargetTypeId FROM OPENJSON(@ExpectedCustomers) WITH (Id bigint,TargetTypeId bigint);
        -- Yalnız müşteri tipi alanı değişir; tenant, grup, finansal kayıt ve kaynak korunur.
        UPDATE c SET CustomerTypeId=CASE WHEN r.Kind=N'Individual' THEN @IndividualId ELSE @GroupId END
        FROM dbo.Customers c JOIN dbo.CustomerGroups g ON g.Id=c.CustomerGroupId
        JOIN @Rules r ON r.Code=UPPER(LTRIM(RTRIM(g.Code))) COLLATE Turkish_CI_AS
        WHERE c.IsDeleted=0 AND c.CustomerTypeId IN (@IndividualId,@GroupId)
        AND c.CustomerTypeId<>CASE WHEN r.Kind=N'Individual' THEN @IndividualId ELSE @GroupId END;
        DECLARE @Changed int=@@ROWCOUNT;
        IF @CurrentDefinition<>@NewDefinition EXEC sys.sp_executesql @NewDefinition;

        IF EXISTS (SELECT 1 FROM @Before b
            LEFT JOIN dbo.Customers c ON c.Id=b.Id
            LEFT JOIN @Expected p ON p.Id=b.Id
            WHERE c.Id IS NULL OR ISNULL(c.CustomerTypeId,-1)<>ISNULL(COALESCE(p.TargetTypeId,b.CustomerTypeId),-1)
                OR ISNULL(c.CustomerGroupId,-1)<>ISNULL(b.CustomerGroupId,-1)
                OR ISNULL(c.TenantId,-1)<>ISNULL(b.TenantId,-1))
            THROW 51000, N'Müşteri güncellemesi doğrulanamadı.', 1;
        SELECT @Changed AS ChangedCustomers,N'Uygulandı' AS Result;
        COMMIT TRANSACTION;
    END
    ELSE
    BEGIN
        SELECT COUNT(*) AS CandidateCustomers,
            SUM(CASE WHEN CustomerTypeId<>TargetTypeId THEN 1 ELSE 0 END) AS ChangedCustomers,
            N'Önizleme; değişiklik yapılmadı' AS Result
        FROM OPENJSON(@ExpectedCustomers) WITH (CustomerTypeId bigint,TargetTypeId bigint);
        ROLLBACK TRANSACTION;
    END;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
