-- Yalnız SELECT/önkoşul kontrolü. Bu dosya restore, DDL veya veri aktarımı yapmaz.
-- Değerler onaylı izole ortam belirlenince açıkça doldurulur; varsayılanlar çalışmayı durdurur.
SET NOCOUNT ON;
SET LOCK_TIMEOUT 5000;
DECLARE @ExpectedServer sysname=NULL;
DECLARE @ProductionServer sysname=NULL;
DECLARE @ExpectedDatabase sysname=N'AssistFlowCollectionRehearsal';
DECLARE @ExpectedSourceDatabase sysname=N'MGSCollectionRehearsal';
DECLARE @IsolationApproved bit=0;
DECLARE @OutboundDenyVerified bit=0;
IF @IsolationApproved<>1 OR @OutboundDenyVerified<>1
 THROW 51000,N'İzolasyon ve dış erişim engeli onaylanmadan prova yapılamaz.',1;
IF NULLIF(@ExpectedServer,N'') IS NULL OR NULLIF(@ProductionServer,N'') IS NULL
 OR @ExpectedServer=@ProductionServer
 OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>@ExpectedServer
 THROW 51000,N'Onaylı ayrı SQL instance doğrulanamadı.',1;
IF @ExpectedDatabase IN(N'AssistFlow',N'AssistFlowTest',N'MGS')
 OR @ExpectedSourceDatabase IN(N'AssistFlow',N'AssistFlowTest',N'MGS')
 OR DB_NAME()<>@ExpectedDatabase OR DB_ID(@ExpectedSourceDatabase) IS NULL
 THROW 51000,N'Prova veritabanı bağlamı veya kaynak restore eksik.',1;
IF EXISTS(SELECT 1 FROM sys.databases WHERE name IN(N'AssistFlow',N'AssistFlowTest',N'MGS'))
 THROW 51000,N'Canlı/test adında DB içeren instance izole prova hedefi olarak kabul edilmez.',1;
IF EXISTS(SELECT 1 FROM sys.servers WHERE is_linked=1)
 THROW 51000,N'Linked server bulunan prova instanceı ayrıca temizlenip doğrulanmalıdır.',1;
IF EXISTS(SELECT 1 FROM sys.databases WHERE name IN(@ExpectedDatabase,@ExpectedSourceDatabase)
 AND (state_desc<>N'ONLINE' OR is_trustworthy_on=1 OR is_db_chaining_on=1))
 THROW 51000,N'Restore durumu veya güvenlik özellikleri uygun değil.',1;
IF ISNULL(HAS_PERMS_BY_NAME(N'msdb',N'DATABASE',N'SELECT'),0)<>1
 THROW 51000,N'Job izolasyonu kontrolü için msdb okuma yetkisi gerekir.',1;
IF EXISTS(SELECT 1 FROM msdb.dbo.sysjobs WHERE enabled=1)
 THROW 51000,N'Etkin SQL Agent Job bulunan instance üzerinde prova başlatılmaz.',1;
SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) ServerName,DB_NAME() DatabaseName,
 SYSUTCDATETIME() CheckedAtUtc;
SELECT name,state_desc,collation_name,recovery_model_desc,is_trustworthy_on,is_db_chaining_on,
 is_read_committed_snapshot_on,snapshot_isolation_state_desc
FROM sys.databases WHERE name IN(@ExpectedDatabase,@ExpectedSourceDatabase);
SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
SELECT s.name SchemaName,t.name TableName FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
 WHERE s.name=N'collection' ORDER BY t.name;
-- Bu SELECT başarısı ağ/firewall, backup bütünlüğü, apply veya geri dönüş kabulü değildir.
