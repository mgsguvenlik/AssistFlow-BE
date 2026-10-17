param(
    [string]$ConfigurationPath = (Join-Path $PSScriptRoot '../WebAPI/appsettings.Production.json'),
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [switch]$CheckDb
)
$ErrorActionPreference = 'Stop'
$targetName = 'AssistFlow_Performance_Restore'
$path = [IO.Path]::GetFullPath($OutputPath)
$privateRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../.tools')) + [IO.Path]::DirectorySeparatorChar
if (-not $path.StartsWith($privateRoot,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $path)) { throw 'Rapor yalnız yeni .tools dosyasına yazılır.' }
$connectionText = [Environment]::GetEnvironmentVariable('COLLECTION_RELEASE_CONNECTIONSTRING')
if ([string]::IsNullOrWhiteSpace($connectionText)) {
    $configuration = Get-Content -LiteralPath $ConfigurationPath -Raw | ConvertFrom-Json
    $connectionText = $configuration.AppSettings.MSSQLConnectionString
}
$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($connectionText)
if ($builder.InitialCatalog -ne 'AssistFlow') { throw 'Kaynak config AssistFlow olmalıdır; hedef adı kodda sabittir.' }
$builder['Initial Catalog'] = $targetName
$builder['ApplicationIntent'] = 'ReadOnly'
$builder['Connect Timeout'] = 10
$builder['Application Name'] = 'CollectionRehearsalTargetReadOnly'
$connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
$command = $connection.CreateCommand()
$command.CommandTimeout = 45
$command.CommandText = @'
SET NOCOUNT ON;
SET LOCK_TIMEOUT 5000;
IF DB_NAME()<>N'AssistFlow_Performance_Restore' THROW 51000,N'Yanlış prova hedefi.',1;
SELECT N'Environment' Section,DB_NAME() DatabaseName,CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) ServerName,
 d.state_desc,d.user_access_desc,d.is_read_only,d.is_trustworthy_on,d.is_db_chaining_on,d.is_broker_enabled,
 d.collation_name,d.create_date,d.recovery_model_desc,HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL') HasDatabaseControl,
 IS_SRVROLEMEMBER('sysadmin') IsSysadmin,SYSUTCDATETIME() CheckedAtUtc
FROM sys.databases d WHERE d.database_id=DB_ID();
SELECT N'Files' Section,name,type_desc,physical_name,size*8./1024 SizeMb FROM sys.database_files ORDER BY file_id;
SELECT N'Tables' Section,s.name SchemaName,t.name TableName,SUM(p.rows) ApproximateRows FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
LEFT JOIN sys.partitions p ON p.object_id=t.object_id AND p.index_id IN(0,1) GROUP BY s.name,t.name ORDER BY s.name,t.name;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory') IS NOT NULL SELECT N'Migrations' Section,MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
SELECT N'ExternalDependencies' Section,OBJECT_SCHEMA_NAME(referencing_id) SchemaName,OBJECT_NAME(referencing_id) ObjectName,
 referenced_server_name,referenced_database_name,referenced_schema_name,referenced_entity_name
FROM sys.sql_expression_dependencies WHERE referenced_database_name IS NOT NULL OR referenced_server_name IS NOT NULL;
SELECT N'Triggers' Section,t.name,t.parent_class_desc,t.is_disabled,OBJECT_SCHEMA_NAME(t.parent_id) ParentSchema,OBJECT_NAME(t.parent_id) ParentObject,
 CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition)),2) DefinitionHash,m.definition
FROM sys.triggers t LEFT JOIN sys.sql_modules m ON m.object_id=t.object_id ORDER BY t.name;
SELECT N'ServerTriggers' Section,name,is_disabled FROM sys.server_triggers;
SELECT N'Modules' Section,s.name SchemaName,o.name ObjectName,CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),m.definition)),2) DefinitionHash,
 CASE WHEN m.definition LIKE N'%AssistFlow%' OR m.definition LIKE N'%MGS%' OR m.definition LIKE N'%OPENQUERY%' OR m.definition LIKE N'%sp_send_dbmail%' OR m.definition LIKE N'%xp_cmdshell%' THEN 1 ELSE 0 END RequiresExternalReview
FROM sys.sql_modules m JOIN sys.objects o ON o.object_id=m.object_id JOIN sys.schemas s ON s.schema_id=o.schema_id ORDER BY s.name,o.name;
IF OBJECT_ID(N'dbo.Customers') IS NOT NULL SELECT N'CustomerCounts' Section,COUNT_BIG(*) Total,SUM(CASE WHEN IsDeleted=0 THEN 1 ELSE 0 END) Active FROM dbo.Customers;
IF OBJECT_ID(N'collection.PaymentFrequency') IS NOT NULL
BEGIN
 SELECT N'SeedSnapshot' Section,N'PaymentFrequency' ObjectName,(SELECT * FROM collection.PaymentFrequency ORDER BY Id FOR JSON PATH) JsonData
 UNION ALL SELECT N'SeedSnapshot',N'ContractStatus',(SELECT * FROM collection.ContractStatus ORDER BY Id FOR JSON PATH)
 UNION ALL SELECT N'SeedSnapshot',N'SubscriptionStatus',(SELECT * FROM collection.SubscriptionStatus ORDER BY Id FOR JSON PATH)
 UNION ALL SELECT N'SeedSnapshot',N'GroupStatus',(SELECT * FROM collection.GroupStatus ORDER BY Id FOR JSON PATH)
 UNION ALL SELECT N'SeedSnapshot',N'PaymentMethod',(SELECT * FROM collection.PaymentMethod ORDER BY Id FOR JSON PATH)
 UNION ALL SELECT N'SeedSnapshot',N'Menu',(SELECT Id,Name,Description FROM dbo.Menus WHERE Name=N'CollectionFollowUp' ORDER BY Id FOR JSON PATH)
 UNION ALL SELECT N'SeedSnapshot',N'SmsTemplate',(SELECT Id,Name,Value,Description FROM dbo.Configurations WHERE Name=N'CollectionRateChangeSmsTemplate' ORDER BY Id FOR JSON PATH)
 UNION ALL SELECT N'SeedSnapshot',N'RolesUnchanged',(SELECT ModulId,RoleId,HasView,HasEdit FROM dbo.MenuRole ORDER BY ModulId,RoleId FOR JSON PATH);
END;
SELECT N'Jobs' Section,j.name JobName,j.enabled,s.step_id,s.database_name,
 CASE WHEN s.database_name=N'AssistFlow_Performance_Restore' OR s.command LIKE N'%AssistFlow_Performance_Restore%' THEN 1 ELSE 0 END ReferencesRehearsal,
 CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),s.command)),2) StepHash
FROM msdb.dbo.sysjobs j JOIN msdb.dbo.sysjobsteps s ON s.job_id=j.job_id ORDER BY j.name,s.step_id;
SELECT TOP(3) N'RestoreHistory' Section,r.restore_date,r.destination_database_name,r.restore_type,b.database_name SourceDatabase,b.backup_finish_date
FROM msdb.dbo.restorehistory r LEFT JOIN msdb.dbo.backupset b ON b.backup_set_id=r.backup_set_id
WHERE r.destination_database_name=N'AssistFlow_Performance_Restore' ORDER BY r.restore_date DESC;
'@
try {
    $connection.Open()
    if($CheckDb){
        $integrity=$connection.CreateCommand();$integrity.CommandTimeout=300
        $integrity.CommandText="IF DB_NAME()<>N'AssistFlow_Performance_Restore' THROW 51000,N'Yanlış bütünlük kontrolü hedefi.',1; DBCC CHECKDB(N'AssistFlow_Performance_Restore') WITH NO_INFOMSGS,ALL_ERRORMSGS,MAXDOP=2;"
        try{$result=$integrity.ExecuteReader();try{$errorRows=0;do{while($result.Read()){$errorRows++}}while($result.NextResult());if($errorRows){throw 'CHECKDB hata satırları döndürdü; sonraki işleme geçilmez.'}}finally{$result.Dispose()}}finally{$integrity.Dispose()}
        Write-Output 'Prova hedefinde CHECKDB tamamlandı; hata satırı yok. REPAIR çalıştırılmadı.'
    }
    $reader=$command.ExecuteReader()
    try {
        $rows=[Collections.Generic.List[object]]::new()
        if($CheckDb){$rows.Add([pscustomobject]@{Section='CheckDb';DatabaseName=$targetName;Completed=$true;ErrorRows=0;RepairExecuted=$false;CheckedAtUtc=[DateTimeOffset]::UtcNow})}
        do { while($reader.Read()) { $row=[ordered]@{}; for($i=0;$i -lt $reader.FieldCount;$i++){ $row[$reader.GetName($i)]=if($reader.IsDBNull($i)){$null}else{$reader.GetValue($i)} }; $rows.Add($row) } } while($reader.NextResult())
        [IO.File]::WriteAllText($path,(ConvertTo-Json -InputObject $rows.ToArray() -Depth 8),[Text.UTF8Encoding]::new($false))
        Write-Output "Prova hedefi salt-okunur incelendi: $targetName. DB/Job değişmedi."
    } finally { $reader.Dispose() }
} catch { throw "Prova ön kontrolü tamamlanamadı; hata türü: $($_.Exception.GetType().Name). Credential raporlanmadı." }
finally { $command.Dispose(); $connection.Dispose() }
