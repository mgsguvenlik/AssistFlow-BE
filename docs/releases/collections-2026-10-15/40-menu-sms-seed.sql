-- Canlıya uygulanmadı. Menü/şablon ekler; mevcut metni veya rol haklarını değiştirmez.
DECLARE @Apply bit=0;
-- Yalnız ayrıca onaylanan rol kodunu girin. NULL: rol yetkisi EKLEMEZ.
DECLARE @ApprovedRoleCode nvarchar(150)=NULL;
IF DB_NAME()<>N'AssistFlow' THROW 51000,N'Hedef AssistFlow olmalıdır.',1;
IF @Apply<>1 THROW 51000,N'Uygulama kapalı; yayın ve yetki onayı gereklidir.',1;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
 BEGIN TRANSACTION;
 DECLARE @lock int;
 EXEC @lock=sys.sp_getapplock @Resource=N'CollectionReleaseMenuSms',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=15000;
 IF @lock<0 THROW 51000,N'Yayın tanım kilidi alınamadı.',1;
 IF (SELECT COUNT(*) FROM dbo.Menus WHERE Name=N'CollectionFollowUp')>1
    THROW 51000,N'Mükerrer Tahsilat menüsü var.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.Menus WHERE Name=N'CollectionFollowUp')
    INSERT dbo.Menus(Name,Description) VALUES(N'CollectionFollowUp',N'Tahsilat sözleşmeleri ve takip');
 IF (SELECT COUNT(*) FROM dbo.Configurations WHERE Name=N'CollectionRateChangeSmsTemplate')>1
    THROW 51000,N'Mükerrer SMS şablonu var.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.Configurations WHERE Name=N'CollectionRateChangeSmsTemplate')
    INSERT dbo.Configurations(Name,Value,Description) VALUES(N'CollectionRateChangeSmsTemplate',
    N'Sayın {MusteriAdi}, {AboneNo} numaralı aboneliğinizin {HizmetAdi} hizmeti için {OdemeDonemi} dönem ücreti %{ZamOrani} artışla {YeniTutar} {ParaBirimi} olacaktır. Yeni ücret {GecerlilikTarihi} tarihinde yürürlüğe girecektir. MGS',
    N'Tahsilat zam SMS şablonu. Parametreler: {MusteriAdi}, {AboneNo}, {HizmetAdi}, {EskiTutar}, {YeniTutar}, {ZamOrani}, {ParaBirimi}, {OdemeDonemi}, {GecerlilikTarihi}.');
 IF @ApprovedRoleCode IS NOT NULL
 BEGIN
    IF (SELECT COUNT(*) FROM dbo.Roles WHERE Code=@ApprovedRoleCode AND IsDeleted=0)<>1
        THROW 51000,N'Onaylanan rol tekil/aktif değil.',1;
    DECLARE @role bigint=(SELECT Id FROM dbo.Roles WHERE Code=@ApprovedRoleCode AND IsDeleted=0);
    DECLARE @menu bigint=(SELECT Id FROM dbo.Menus WHERE Name=N'CollectionFollowUp');
    IF EXISTS(SELECT 1 FROM dbo.MenuRole WHERE RoleId=@role AND ModulId=@menu)
    BEGIN
        IF (SELECT COUNT(*) FROM dbo.MenuRole WHERE RoleId=@role AND ModulId=@menu)<>1
            THROW 51000,N'Mükerrer menü yetkisi var.',1;
        IF EXISTS(SELECT 1 FROM dbo.MenuRole WHERE RoleId=@role AND ModulId=@menu AND (HasView<>1 OR HasEdit<>1))
            THROW 51000,N'Mevcut rol hakkı farklı; ayrı yetki değişikliği onayı gerekir.',1;
    END
    ELSE INSERT dbo.MenuRole(ModulId,RoleId,HasView,HasEdit) VALUES(@menu,@role,1,1);
 END;
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
