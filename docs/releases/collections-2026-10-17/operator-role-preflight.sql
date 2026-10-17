-- Salt-okuma onay ön kontrolü. Bu çalışmada çalıştırılmadı.
-- Kimlik ve rol bilgisi içeren sonuçlar Git dışında korunmalıdır.
SET NOCOUNT ON;
SET LOCK_TIMEOUT 5000;
IF DB_NAME()<>N'AssistFlow'
 OR UPPER(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')))<>N'SQL2022'
 THROW 51000,N'Yanlış canlı ön kontrol hedefi.',1;

DECLARE @OperatorCode nvarchar(50)=NULL;
DECLARE @ReviewedRoleCode nvarchar(50)=NULL;

SELECT N'OperatorDecision' AS Section,
 CASE WHEN @OperatorCode IS NULL THEN N'OPERATÖR KODU BEKLENİYOR'
      WHEN (SELECT COUNT_BIG(*) FROM dbo.Users WHERE Code=@OperatorCode AND IsDeleted=0)=1
      THEN N'TEKİL AKTİF OPERATÖR BULUNDU' ELSE N'DUR: OPERATÖR TEKİL/AKTİF DEĞİL' END AS Result;
SELECT N'SystemActorCheck' AS Section,
 CASE WHEN EXISTS(SELECT 1 FROM dbo.Users WHERE Id=0) THEN N'DUR: TEKNİK AKTÖR ÇAKIŞMASI'
 ELSE N'TEKNİK AKTÖR 0 GERÇEK KULLANICI DEĞİL' END AS Result;
SELECT N'Operator' AS Section,Id,Code,IsDeleted FROM dbo.Users
 WHERE @OperatorCode IS NOT NULL AND Code=@OperatorCode;

SELECT N'RoleCandidates' AS Section,r.Id,r.Code,r.Name,r.IsDeleted,
 COUNT_BIG(ur.Id) AS MembershipCount
 FROM dbo.Roles r LEFT JOIN dbo.UserRole ur ON ur.RoleId=r.Id
 WHERE @ReviewedRoleCode IS NULL OR r.Code=@ReviewedRoleCode
 GROUP BY r.Id,r.Code,r.Name,r.IsDeleted ORDER BY r.Code,r.Id;
SELECT N'NonUniqueActiveRoleCode' AS Section,Code,COUNT_BIG(*) AS Occurrences
 FROM dbo.Roles WHERE IsDeleted=0 GROUP BY Code
 HAVING Code IS NULL OR LTRIM(RTRIM(Code))=N'' OR COUNT_BIG(*)>1;
SELECT N'CollectionMenu' AS Section,Id,Name,Description FROM dbo.Menus
 WHERE Name=N'CollectionFollowUp' ORDER BY Id;
SELECT N'ExistingCollectionRights' AS Section,r.Id AS RoleId,r.Code,r.IsDeleted,
 mr.Id AS MenuRoleId,mr.ModulId,mr.HasView,mr.HasEdit
 FROM dbo.MenuRole mr JOIN dbo.Menus m ON m.Id=mr.ModulId
 JOIN dbo.Roles r ON r.Id=mr.RoleId
 WHERE m.Name=N'CollectionFollowUp' AND (@ReviewedRoleCode IS NULL OR r.Code=@ReviewedRoleCode)
 ORDER BY r.Code,mr.Id;
SELECT N'DuplicateCollectionRights' AS Section,mr.RoleId,mr.ModulId,COUNT_BIG(*) AS Occurrences
 FROM dbo.MenuRole mr JOIN dbo.Menus m ON m.Id=mr.ModulId
 WHERE m.Name=N'CollectionFollowUp' GROUP BY mr.RoleId,mr.ModulId HAVING COUNT_BIG(*)>1;
SELECT N'ReviewedRoleMembers' AS Section,r.Code AS RoleCode,r.IsDeleted AS RoleIsDeleted,
 u.Code AS UserCode,u.IsDeleted AS UserIsDeleted
 FROM dbo.Roles r JOIN dbo.UserRole ur ON ur.RoleId=r.Id JOIN dbo.Users u ON u.Id=ur.UserId
 WHERE @ReviewedRoleCode IS NOT NULL AND r.Code=@ReviewedRoleCode ORDER BY u.Code;

-- Etkin hak tüm rollerin birleşimidir; seçilen tek role daraltılmaz.
-- Mevcut MenuAuthorizationFilter rolün soft-delete durumunu ayrıca süzmüyor.
SELECT N'OperatorEffectiveCollectionPermission' AS Section,u.Id,u.Code,
 COALESCE(MAX(CASE WHEN m.Name=N'CollectionFollowUp' AND mr.HasView=1 THEN 1 ELSE 0 END),0) AS EffectiveView,
 COALESCE(MAX(CASE WHEN m.Name=N'CollectionFollowUp' AND mr.HasEdit=1 THEN 1 ELSE 0 END),0) AS EffectiveEdit
 FROM dbo.Users u LEFT JOIN dbo.UserRole ur ON ur.UserId=u.Id
 LEFT JOIN dbo.MenuRole mr ON mr.RoleId=ur.RoleId LEFT JOIN dbo.Menus m ON m.Id=mr.ModulId
 WHERE @OperatorCode IS NOT NULL AND u.Code=@OperatorCode GROUP BY u.Id,u.Code;

SELECT N'TypeBaseline' AS Section,Id,Code,Name FROM dbo.CustomerType
 WHERE Id IN(4,6,7) OR Code IN(N'BRYSL01',N'BNK01',N'GRP01',N'G') ORDER BY Id;
SELECT N'ServiceBaseline' AS Section,Id,Name,IsDeleted FROM dbo.ServiceType ORDER BY Id;
SELECT N'SyncProcedureHash' AS Section,p.DefinitionHash,
 CASE WHEN p.DefinitionHash=N'7E6F301164802E3DCA96C6A8291D31CF61B160A4DB41047AD6505A545D81315C' THEN N'BEKLENEN ÖNCEKİ SÜRÜM'
 WHEN p.DefinitionHash=N'C7A77A88BEE20F2B90858100B32F2BA1E4ADD16208D530B44287D40CB30F1A2C' THEN N'İNCELENEN YAMA MEVCUT'
 ELSE N'DUR: PROSEDÜR SÜRÜMÜ YENİDEN İNCELENMELİ' END AS Result
 FROM (SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),
 OBJECT_DEFINITION(OBJECT_ID(N'stg.sp_SyncCustomers')))),2) AS DefinitionHash) p;
