param([Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$output=[IO.Path]::GetFullPath((Join-Path $repo $OutputPath))
if(-not $output.StartsWith((Join-Path $repo '.tools')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $output)){throw 'Yeni özel .tools raporu gerekli.'}
$config=Get-Content -LiteralPath (Join-Path $repo 'WebAPI/appsettings.Production.json') -Raw|ConvertFrom-Json
$text=[Environment]::GetEnvironmentVariable('COLLECTION_RELEASE_CONNECTIONSTRING')
if([string]::IsNullOrWhiteSpace($text)){$text=$config.AppSettings.MSSQLConnectionString}
$b=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($text)
if($b.InitialCatalog -ne 'AssistFlow' -or $b.DataSource -notin @('192.168.1.8','sql2022')){throw 'Yanlış canlı salt-okuma hedefi.'}
$b['ApplicationIntent']='ReadOnly';$b['Application Name']='CollectionCdnReadOnlyInventory';$b['Connect Timeout']=10
$connection=[System.Data.SqlClient.SqlConnection]::new($b.ConnectionString)
try{
 $connection.Open();$command=$connection.CreateCommand();$command.CommandTimeout=30
 $command.CommandText=@'
SET NOCOUNT ON; SET LOCK_TIMEOUT 5000;
IF DB_NAME()<>N'AssistFlow' OR UPPER(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')))<>N'SQL2022' THROW 51000,N'Yanlış salt-okuma hedefi.',1;
SELECT N'Environment' Section,DB_NAME() DatabaseName,SYSUTCDATETIME() ObservedAtUtc;
SELECT N'FileColumns' Section,s.name SchemaName,t.name TableName,c.name ColumnName,ty.name TypeName
FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
WHERE t.is_ms_shipped=0 AND (c.name IN(N'StoredFileName',N'FileName',N'FilePath',N'FileUrl',N'ImagePath',N'ImageUrl',N'AttachmentPath',N'ContentType',N'SizeBytes',N'OriginalFileName')
 OR (t.name LIKE N'%Attachment%' OR t.name LIKE N'%Image%') AND c.name IN(N'Id',N'IsDeleted',N'Path',N'Url'))
ORDER BY s.name,t.name,c.column_id;
SELECT N'CollectionFileTables' Section,s.name SchemaName,t.name TableName FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
WHERE s.name=N'collection' AND t.name IN(N'ContractAttachment',N'CustomerAttachment');
'@
 $reader=$command.ExecuteReader();$rows=[Collections.Generic.List[object]]::new()
 try{do{while($reader.Read()){$row=[ordered]@{};for($i=0;$i -lt $reader.FieldCount;$i++){$row[$reader.GetName($i)]=if($reader.IsDBNull($i)){$null}else{$reader.GetValue($i)}};$rows.Add($row)}}while($reader.NextResult())}finally{$reader.Dispose()}
 [IO.File]::WriteAllText($output,(ConvertTo-Json -InputObject $rows.ToArray() -Depth 6),[Text.UTF8Encoding]::new($false))
 'Salt-okunur CDN tablo envanteri kaydedildi; DB değişmedi.'
}finally{$connection.Dispose()}
