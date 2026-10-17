param([Parameter(Mandatory=$true)][string]$PackageDirectory,[Parameter(Mandatory=$true)][string]$ExpectedManifestHash,[Parameter(Mandatory=$true)][string]$OutputPath,[string]$ConfigurationPath=(Join-Path $PSScriptRoot '../WebAPI/appsettings.Production.json'))
$ErrorActionPreference='Stop'
$privateRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../.tools'))+[IO.Path]::DirectorySeparatorChar
$root=[IO.Path]::GetFullPath($PackageDirectory);$out=[IO.Path]::GetFullPath($OutputPath)
if(-not $root.StartsWith($privateRoot,[StringComparison]::OrdinalIgnoreCase) -or -not $out.StartsWith($privateRoot,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $out)){throw 'Yalnız özel paket/yeni kanıt dosyası kullanılabilir.'}
if((Get-FileHash (Join-Path $root 'manifest.json') -Algorithm SHA256).Hash -ne $ExpectedManifestHash){throw 'Prova manifest hash uyuşmazlığı.'}
$manifest=Get-Content (Join-Path $root 'manifest.json') -Raw|ConvertFrom-Json
if($manifest.Status -ne 'REHEARSAL_ONLY_NO_PRODUCTION_APPLY' -or $manifest.Target -ne 'AssistFlow_Performance_Restore' -or $manifest.Server -ne 'sql2022'){throw 'Prova paketi hedefi uygun değil.'}
foreach($f in $manifest.Files){if([IO.Path]::GetFileName($f.File) -ne $f.File -or (Get-FileHash (Join-Path $root $f.File) -Algorithm SHA256).Hash -ne $f.Sha256){throw 'Prova SQL hash/yol uyuşmazlığı.'}}
$cs=[Environment]::GetEnvironmentVariable('COLLECTION_RELEASE_CONNECTIONSTRING')
if([string]::IsNullOrWhiteSpace($cs)){$cfg=Get-Content $ConfigurationPath -Raw|ConvertFrom-Json;$cs=$cfg.AppSettings.MSSQLConnectionString}
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($cs)
if($builder.InitialCatalog -ne 'AssistFlow'){throw 'Kaynak config uygun değil.'}
$builder['Initial Catalog']='AssistFlow_Performance_Restore';$builder['Application Name']='CollectionRehearsalSchemaOnly';$builder['Connect Timeout']=10
$db=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
function ReadMigrations(){
    $cmd=$db.CreateCommand();$cmd.CommandText='SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId';$cmd.CommandTimeout=30
    try{$r=$cmd.ExecuteReader();try{$list=[Collections.Generic.List[string]]::new();while($r.Read()){$list.Add($r.GetString(0))};return ,$list.ToArray()}finally{$r.Dispose()}}finally{$cmd.Dispose()}
}
$completed=[Collections.Generic.List[object]]::new();$before=@();$success=$false
try{
    $db.Open()
    $guard=$db.CreateCommand();$guard.CommandText="IF DB_NAME()<>N'AssistFlow_Performance_Restore' OR CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))<>N'sql2022' THROW 51000,N'Yanlış prova bağlantısı.',1; IF EXISTS(SELECT 1 FROM sys.triggers WHERE is_disabled=0) OR EXISTS(SELECT 1 FROM sys.server_triggers WHERE is_disabled=0) THROW 51000,N'Etkin trigger incelemesi gerekir.',1;"
    try{$null=$guard.ExecuteNonQuery()}finally{$guard.Dispose()}
    $before=ReadMigrations
    $sameBaseline=([string]::Join('|',$before) -eq [string]::Join('|',[string[]]$manifest.ExpectedBaselineMigrations))
    $sameAfter=([string]::Join('|',$before) -eq [string]::Join('|',[string[]]$manifest.ExpectedAfterMigrations))
    if(-not $sameBaseline -and -not $sameAfter){throw 'Hedef migration geçmişi baseline/tekrar durumuyla uyuşmuyor; kör devam edilmedi.'}
    foreach($f in $manifest.Files){
        $sql=Get-Content (Join-Path $root $f.File) -Raw
        foreach($batch in [regex]::Split($sql,'(?im)^\s*GO\s*(?:--[^\r\n]*)?\r?$')){
            if([string]::IsNullOrWhiteSpace($batch)){continue}
            if(-not $batch.Contains("DB_NAME()<>N'AssistFlow_Performance_Restore'")){throw 'Batch hedef guardı eksik.'}
            $cmd=$db.CreateCommand();$cmd.CommandText=$batch;$cmd.CommandTimeout=120
            try{$null=$cmd.ExecuteNonQuery()}finally{$cmd.Dispose()}
        }
        $completed.Add([pscustomobject]@{File=$f.File;Sha256=$f.Sha256;CompletedAtUtc=[DateTimeOffset]::UtcNow})
        Write-Output "Prova SQL tamamlandı: $($f.File)"
    }
    $after=ReadMigrations
    if([string]::Join('|',$after) -ne [string]::Join('|',[string[]]$manifest.ExpectedAfterMigrations)){throw 'Şema sonrası migration seti uyuşmuyor.'}
    $success=$true
}catch{
    if($db.State -eq 'Open'){$rollback=$db.CreateCommand();$rollback.CommandText='IF @@TRANCOUNT>0 ROLLBACK;';try{$null=$rollback.ExecuteNonQuery()}catch{}finally{$rollback.Dispose()}}
    throw
}finally{
    [IO.File]::WriteAllText($out,(ConvertTo-Json -InputObject ([ordered]@{Success=$success;Target='AssistFlow_Performance_Restore';PackageHash=$ExpectedManifestHash;BeforeMigrations=$before;CompletedFiles=$completed.ToArray();AtUtc=[DateTimeOffset]::UtcNow;ProductionWrites=$false}) -Depth 8),[Text.UTF8Encoding]::new($false))
    $db.Dispose()
}
