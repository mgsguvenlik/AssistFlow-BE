param(
    [string]$ConfigurationPath = (Join-Path $PSScriptRoot '../WebAPI/appsettings.Production.json'),
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$ExpectedPlanHash
)
$ErrorActionPreference='Stop'
$target='AssistFlow_Performance_Restore'
$expectedServer='sql2022'
$root=[IO.Path]::GetFullPath($OutputDirectory)
$privateRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../.tools'))+[IO.Path]::DirectorySeparatorChar
if(-not $root.StartsWith($privateRoot,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $root)){throw 'Yeni özel çıktı klasörü gerekir.'}
$cs=[Environment]::GetEnvironmentVariable('COLLECTION_RELEASE_CONNECTIONSTRING')
if([string]::IsNullOrWhiteSpace($cs)){$cfg=Get-Content -LiteralPath $ConfigurationPath -Raw|ConvertFrom-Json;$cs=$cfg.AppSettings.MSSQLConnectionString}
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($cs)
if($builder.InitialCatalog -ne 'AssistFlow'){throw 'Kaynak config AssistFlow olmalıdır.'}
$builder['Initial Catalog']='master'
$builder['Connect Timeout']=10
$builder['Application Name']='CollectionRehearsalRestoreControlled'
$connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
$observer=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
function Query([string]$sql){
    $cmd=$connection.CreateCommand();$cmd.CommandText=$sql;$cmd.CommandTimeout=45
    try{$reader=$cmd.ExecuteReader();try{$rows=[Collections.Generic.List[object]]::new();while($reader.Read()){$row=[ordered]@{};for($i=0;$i -lt $reader.FieldCount;$i++){$row[$reader.GetName($i)]=if($reader.IsDBNull($i)){$null}else{$reader.GetValue($i)}};$rows.Add([pscustomobject]$row)};return ,$rows.ToArray()}finally{$reader.Dispose()}}finally{$cmd.Dispose()}
}
function Save([string]$name,$value){[IO.File]::WriteAllText((Join-Path $root $name),(ConvertTo-Json -InputObject $value -Depth 12),[Text.UTF8Encoding]::new($false))}
function HashText([string]$text){[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text)))}
function LongOperation([string]$sql,[int]$spid,[string]$label){
    $cmd=$connection.CreateCommand();$cmd.CommandText=$sql;$cmd.CommandTimeout=0
    try{
        $task=$cmd.ExecuteNonQueryAsync();$watch=[Diagnostics.Stopwatch]::StartNew();$last=0
        while(-not $task.IsCompleted){
            Start-Sleep -Milliseconds 500
            if($watch.Elapsed.TotalSeconds-$last -ge 15){
                $last=$watch.Elapsed.TotalSeconds
                $probe=$observer.CreateCommand();$probe.CommandTimeout=10;$probe.CommandText="SELECT command,percent_complete,estimated_completion_time FROM sys.dm_exec_requests WHERE session_id=$spid"
                try{$r=$probe.ExecuteReader();try{if($r.Read()){Write-Output ("{0}: {1:N1}% ({2})" -f $label,[double]$r.GetValue(1),$r.GetString(0))}}finally{$r.Dispose()}}catch{Write-Output "$label devam ediyor; ilerleme okunamadı."}finally{$probe.Dispose()}
            }
        }
        $null=$task.GetAwaiter().GetResult()
        Write-Output "$label tamamlandı."
    }finally{$cmd.Dispose()}
}
$restoreStarted=$false
try{
    $connection.Open();$observer.Open()
    $env=Query "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) ServerName,@@SPID Spid"
    if($env[0].ServerName -ne $expectedServer){throw 'Beklenen SQL instance değil.'}
    $targetInfo=Query "SELECT name,state_desc,user_access_desc,is_trustworthy_on,is_db_chaining_on FROM sys.databases WHERE name=N'AssistFlow_Performance_Restore'"
    if($targetInfo.Count -ne 1 -or $targetInfo[0].state_desc -ne 'ONLINE' -or $targetInfo[0].user_access_desc -ne 'MULTI_USER'){throw 'Mevcut prova hedefi ONLINE/MULTI_USER değil.'}
    $files=Query "SELECT f.file_id,f.name,f.type,f.physical_name,CAST(f.size AS bigint)*8192 ExistingBytes,v.volume_mount_point,v.available_bytes FROM sys.master_files f CROSS APPLY sys.dm_os_volume_stats(f.database_id,f.file_id) v WHERE f.database_id=DB_ID(N'AssistFlow_Performance_Restore') ORDER BY f.file_id"
    if($files.Count -ne 2 -or @($files|Where-Object type -eq 0).Count -ne 1 -or @($files|Where-Object type -eq 1).Count -ne 1){throw 'Beklenen iki hedef dosya yapısı değişmiş.'}
    $requiredPaths=@('H:\sqldata\AssistFlow_Performance_Restore.mdf','G:\sqllog\AssistFlow_Performance_Restore_log.ldf')
    foreach($f in $files){if($f.physical_name -notin $requiredPaths){throw 'Hedef dosya yolu izinli prova yolu değil.'}}
    $collisions=Query "SELECT database_id,physical_name FROM sys.master_files WHERE physical_name IN(N'H:\sqldata\AssistFlow_Performance_Restore.mdf',N'G:\sqllog\AssistFlow_Performance_Restore_log.ldf') AND database_id<>DB_ID(N'AssistFlow_Performance_Restore')"
    if($collisions.Count){throw 'Başka DB ile fiziksel dosya çakışması.'}
    $sessions=Query "SELECT session_id FROM sys.dm_exec_sessions WHERE is_user_process=1 AND database_id=DB_ID(N'AssistFlow_Performance_Restore') AND session_id<>@@SPID"
    if($sessions.Count){throw 'Prova hedefinde açık kullanıcı oturumu var; zorla sonlandırılmadı.'}
    $backup=Query "SELECT TOP(1) b.backup_set_id,b.position,b.backup_set_uuid,b.backup_start_date,b.backup_finish_date,b.has_backup_checksums,b.is_damaged,b.media_set_id,m.physical_device_name FROM msdb.dbo.backupset b JOIN msdb.dbo.backupmediafamily m ON m.media_set_id=b.media_set_id WHERE b.database_name=N'AssistFlow' AND b.type='D' AND b.backup_finish_date IS NOT NULL AND b.is_damaged=0 ORDER BY b.backup_finish_date DESC,b.backup_set_id DESC,m.family_sequence_number"
    if($backup.Count -ne 1){throw 'AssistFlow tam yedeği bulunamadı.'}
    $families=Query ("SELECT physical_device_name FROM msdb.dbo.backupmediafamily WHERE media_set_id="+[long]$backup[0].media_set_id)
    if($families.Count -ne 1){throw 'Çok parçalı yedek için ayrı açık restore planı gerekir.'}
    $disk=([string]$backup[0].physical_device_name).Replace("'","''")
    $position=[int]$backup[0].position
    $header=Query "RESTORE HEADERONLY FROM DISK=N'$disk'"
    $selected=@($header|Where-Object Position -eq $position)
    if($selected.Count -ne 1 -or $selected[0].DatabaseName -ne 'AssistFlow' -or $selected[0].BackupType -ne 1 -or [string]$selected[0].BackupSetGUID -ne [string]$backup[0].backup_set_uuid){throw 'Yedek başlığı canlı AssistFlow metadata ile uyuşmuyor.'}
    $list=Query "RESTORE FILELISTONLY FROM DISK=N'$disk' WITH FILE=$position"
    if($list.Count -ne 2 -or @($list|Where-Object Type -eq 'D').Count -ne 1 -or @($list|Where-Object Type -eq 'L').Count -ne 1){throw 'Yedek dosya listesi için iki açık MOVE eşlemesi kurulamadı.'}
    $moves=foreach($f in $files){$source=@($list|Where-Object Type -eq $(if($f.type -eq 0){'D'}else{'L'}))[0];[pscustomobject]@{LogicalName=$source.LogicalName;TargetPath=$f.physical_name;BackupBytes=[long]$source.Size;ExistingBytes=[long]$f.ExistingBytes;Volume=$f.volume_mount_point;AvailableBytes=[long]$f.available_bytes}}
    foreach($g in ($moves|Group-Object Volume)){$growth=($g.Group|ForEach-Object{[Math]::Max([long]0,[long]($_.BackupBytes-$_.ExistingBytes))}|Measure-Object -Sum).Sum;if([long]$g.Group[0].AvailableBytes -lt ($growth+1GB)){throw 'Hedef volume üzerinde güvenli büyüme alanı yok.'}}
    $material=[ordered]@{Server=$expectedServer;Target=$target;TargetFiles=$files|Select-Object file_id,name,type,physical_name,ExistingBytes;Backup=$backup[0];Header=$selected[0];Moves=$moves|Select-Object LogicalName,TargetPath,BackupBytes,ExistingBytes,Volume}
    $json=ConvertTo-Json -InputObject $material -Depth 12 -Compress
    $hash=HashText $json
    $null=New-Item -ItemType Directory -Path $root
    Save 'restore-plan.json' ([ordered]@{PlanHash=$hash;Applied=$false;Plan=$material})
    Write-Output "Restore planı hazır; kaynak AssistFlow tam yedeği: $($backup[0].backup_finish_date). Plan SHA-256: $hash"
    if([string]::IsNullOrWhiteSpace($ExpectedPlanHash)){Write-Output 'Yalnız plan üretildi; DB değiştirilmedi.';return}
    if($ExpectedPlanHash -ne $hash){throw 'Onaylı plan hash uyuşmuyor; restore yapılmadı.'}
    LongOperation "RESTORE VERIFYONLY FROM DISK=N'$disk' WITH FILE=$position" ([int]$env[0].Spid) 'Yedek VERIFYONLY'
    Save 'verifyonly.json' @{VerifiedAtUtc=[DateTimeOffset]::UtcNow;PlanHash=$hash;BackupChecksums=[bool]$backup[0].has_backup_checksums;Note='VERIFYONLY başarılı olması CHECKDB veya checksum yokken checksum doğrulaması değildir.'}
    $moveSql=($moves|ForEach-Object{"MOVE N'$(([string]$_.LogicalName).Replace("'","''"))' TO N'$(([string]$_.TargetPath).Replace("'","''"))'"}) -join ','
    $sql=@"
IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'sql2022' OR DB_NAME()<>N'master' THROW 51000,N'Yanlış restore bağlantısı.',1;
IF EXISTS(SELECT 1 FROM sys.master_files WHERE physical_name IN(N'H:\sqldata\AssistFlow_Performance_Restore.mdf',N'G:\sqllog\AssistFlow_Performance_Restore_log.ldf') AND database_id<>DB_ID(N'AssistFlow_Performance_Restore')) THROW 51000,N'Hedef dosya çakışması.',1;
IF EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE is_user_process=1 AND database_id=DB_ID(N'AssistFlow_Performance_Restore') AND session_id<>@@SPID) THROW 51000,N'Hedefe yeni oturum açılmış.',1;
ALTER DATABASE [AssistFlow_Performance_Restore] SET SINGLE_USER WITH NO_WAIT;
RESTORE DATABASE [AssistFlow_Performance_Restore] FROM DISK=N'$disk' WITH FILE=$position,$moveSql,REPLACE,RECOVERY,STATS=5;
ALTER DATABASE [AssistFlow_Performance_Restore] SET MULTI_USER;
"@
    Save 'restore-started.json' @{StartedAtUtc=[DateTimeOffset]::UtcNow;PlanHash=$hash;Target=$target}
    $restoreStarted=$true
    LongOperation $sql ([int]$env[0].Spid) 'Prova DB restore'
    $post=Query "SELECT name,state_desc,user_access_desc,is_trustworthy_on,is_db_chaining_on,is_broker_enabled FROM sys.databases WHERE name=N'AssistFlow_Performance_Restore'"
    $postFiles=Query "SELECT name,physical_name FROM sys.master_files WHERE database_id=DB_ID(N'AssistFlow_Performance_Restore') ORDER BY file_id"
    if($post.Count -ne 1 -or $post[0].state_desc -ne 'ONLINE' -or $post[0].user_access_desc -ne 'MULTI_USER'){throw 'Restore sonrası hedef durumu doğrulanamadı.'}
    Save 'restore-completed.json' @{CompletedAtUtc=[DateTimeOffset]::UtcNow;PlanHash=$hash;Target=$post;Files=$postFiles;SourceDatabaseWrites=$false;CheckDbComplete=$false}
    Write-Output 'Yalnız AssistFlow_Performance_Restore güncel canlı yedeğinden restore edildi. CHECKDB ve prova kabulü ayrı adımdır.'
}catch{
    if(Test-Path -LiteralPath $root){Save 'failure.json' @{AtUtc=[DateTimeOffset]::UtcNow;RestoreStarted=$restoreStarted;ExceptionType=$_.Exception.GetType().Name;Note='Hata sonrası hedef durumu salt okunur incelenmeli; otomatik DROP/yeniden restore/kör MULTI_USER yapılmaz.'}}
    throw $_
}finally{$connection.Dispose();$observer.Dispose()}
