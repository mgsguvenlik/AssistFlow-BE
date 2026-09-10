param([switch]$Apply)

$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$scriptPath = Join-Path $repository 'docs/sql/20260910150310_AddCollectionFoundation.sql'
$expectedHash = 'A4DAE537BFD45CD2589685DE6E0F41F060BA07281EDD9E24A7E6A7DB7EFC7351'
if ((Get-FileHash -LiteralPath $scriptPath -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'İncelenen SQL dosyası değişmiş. Yeniden inceleme yapılmadan kurulum uygulanamaz.'
}
$configuration = Get-Content (Join-Path $repository 'WebAPI/appsettings.Development.json') -Raw | ConvertFrom-Json
$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($configuration.AppSettings.MSSQLConnectionString)
if ($builder.DataSource -ne '192.168.1.8' -or $builder.InitialCatalog -ne 'AssistFlowTest') {
    throw 'Bu kurulum yalnız onaylı 192.168.1.8 / AssistFlowTest hedefinde çalışır.'
}
$builder['Connect Timeout'] = 10
$connection = New-Object System.Data.SqlClient.SqlConnection($builder.ConnectionString)
$transaction = $null
function Invoke-CollectionSql([string]$Sql) {
    $command = $connection.CreateCommand()
    try {
        $command.CommandTimeout = 60
        $command.Transaction = $transaction
        $command.CommandText = $Sql
        [void]$command.ExecuteNonQuery()
    } finally { $command.Dispose() }
}
try {
    $connection.Open()
    $transaction = $connection.BeginTransaction()
    Invoke-CollectionSql @'
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'AssistFlowTest' THROW 51000, N'Hedef veritabanı uygun değil.', 1;
DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock @Resource=N'CollectionFoundationInstall', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
IF @lockResult < 0 THROW 51000, N'Kurulum kilidi alınamadı.', 1;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NULL THROW 51000, N'Migration geçmişi bulunamadı.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260905164011_AddEkbTenantModule')
    THROW 51000, N'Beklenen temel migration bulunamadı.', 1;
IF EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260910150310_AddCollectionFoundation')
    THROW 51000, N'Tahsilat kurulumu zaten kayıtlı. Yeniden uygulanmadı.', 1;
IF EXISTS (SELECT 1 FROM sys.objects WHERE schema_id=SCHEMA_ID(N'collection') AND is_ms_shipped=0)
    THROW 51000, N'Collection şemasında mevcut nesneler var. Önce şema uyumu incelenmeli.', 1;
IF OBJECT_ID(N'Customers') <> OBJECT_ID(N'dbo.Customers') OR OBJECT_ID(N'ServiceType') <> OBJECT_ID(N'dbo.ServiceType')
    OR OBJECT_ID(N'CurrencyType') <> OBJECT_ID(N'dbo.CurrencyType') OR OBJECT_ID(N'__EFMigrationsHistory') <> OBJECT_ID(N'dbo.__EFMigrationsHistory')
    THROW 51000, N'Varsayılan şema ortak tablolarla uyuşmuyor.', 1;
IF (SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id
    WHERE t.schema_id=SCHEMA_ID(N'dbo') AND t.name IN (N'Customers',N'ServiceType',N'CurrencyType')
    AND c.name=N'Id' AND TYPE_NAME(c.user_type_id)=N'bigint' AND c.is_nullable=0) <> 3
    THROW 51000, N'Ortak tablo anahtarları beklenen yapıda değil.', 1;
'@
    if (-not $Apply) {
        $transaction.Rollback()
        Write-Output 'Ön kontrol başarılı. Değişiklik yapılmadı. Kurulum için -Apply kullanılır.'
        return
    }
    $sql = Get-Content -LiteralPath $scriptPath -Raw
    foreach ($batch in [regex]::Split($sql, '(?m)^GO\s*\r?$')) {
        if (-not [string]::IsNullOrWhiteSpace($batch)) { Invoke-CollectionSql $batch }
    }
    Invoke-CollectionSql @'
IF (SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(N'collection')) <> 10
    THROW 51000, N'Tablo sayısı doğrulanamadı.', 1;
IF (SELECT COUNT(*) FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id
    WHERE t.schema_id=SCHEMA_ID(N'collection') AND i.index_id>0 AND i.is_primary_key=0) <> 21
    THROW 51000, N'İndeks sayısı doğrulanamadı.', 1;
IF (SELECT COUNT(*) FROM sys.foreign_keys WHERE schema_id=SCHEMA_ID(N'collection')) <> 12
    THROW 51000, N'İlişki sayısı doğrulanamadı.', 1;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE schema_id=SCHEMA_ID(N'collection')
    AND (is_disabled=1 OR is_not_trusted=1 OR delete_referential_action<>0))
    THROW 51000, N'İlişki güvenliği doğrulanamadı.', 1;
IF (SELECT COUNT(*) FROM sys.check_constraints WHERE schema_id=SCHEMA_ID(N'collection') AND is_disabled=0 AND is_not_trusted=0) <> 14
    THROW 51000, N'Veri kısıtları doğrulanamadı.', 1;
'@
    $transaction.Commit()
    Write-Output 'Tahsilat temel kurulumu tamamlandı: 10 tablo, 21 indeks, 12 FK, 14 CHECK. Yalnız collection nesneleri ve yeni migration geçmişi kaydı eklendi.'
} catch {
    if ($transaction -and $transaction.Connection) { $transaction.Rollback() }
    throw
} finally {
    if ($transaction) { $transaction.Dispose() }
    $connection.Dispose()
}
